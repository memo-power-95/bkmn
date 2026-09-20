using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Rebith.Core
{
    public class ResultadoImportacion
    {
        public int Objetivos;
        public int Respaldos;
        public int Archivos;
        public int Contenidos;
        public int Usuarios;
        public int EventosAuditoria;
        public List<string> Avisos = new List<string>();
    }

    /// <summary>
    /// Trae al deposito nuevo los respaldos de la version en Python (backups.db + carpeta
    /// storage). NO modifica nada de la version anterior: solo lee y copia. Cada contenido
    /// copiado se comprueba con su huella antes de registrarlo. Se puede correr dos veces:
    /// lo que ya se importo no se duplica.
    /// </summary>
    public class Importador
    {
        private readonly RebithService servicio;

        public Importador(RebithService servicio)
        {
            this.servicio = servicio;
        }

        /// <summary>Ubicacion normal de la version anterior en esta PC.</summary>
        public static string BaseAnteriorPredeterminada()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "rebith", "backups.db");
        }

        public ResultadoImportacion Importar(string rutaDb, string usuario, IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            if (!File.Exists(rutaDb)) throw new RebithException("No existe " + rutaDb);
            string carpetaStorage = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rutaDb)), "storage");
            var lector = new SqliteLector(rutaDb);
            if (!lector.ExisteTabla("targets") || !lector.ExisteTabla("snapshots"))
                throw new RebithException("El archivo no parece una base de la version anterior de Rebith.");

            var r = new ResultadoImportacion();
            List<Dictionary<string, object>> targets = lector.LeerTabla("targets");
            List<Dictionary<string, object>> snapshots = lector.LeerTabla("snapshots");
            Dictionary<string, Dictionary<string, object>> archivos = lector.LeerTabla("files")
                .GroupBy(f => Texto(f, "hash")).ToDictionary(g => g.Key, g => g.First());
            ILookup<long, Dictionary<string, object>> versiones = lector.LeerTabla("file_versions").ToLookup(v => Numero(v, "snapshot_id"));

            using (RepoLock candado = servicio.Repo.TomarCandado("Importar version anterior", usuario))
            {
                Repository repo = servicio.Repo;
                var yaImportados = new HashSet<string>(repo.LeerManifiestos().Manifiestos.Select(m => m.Id));
                var mapaObjetivos = new Dictionary<long, TargetInfo>();

                // La version en Python creaba un objetivo nuevo en cada respaldo manual: aqui se
                // juntan los que apuntan a la misma ruta.
                foreach (var t in targets.OrderBy(x => Numero(x, "id")))
                {
                    string ruta = Texto(t, "source_path");
                    TargetInfo existente = repo.LeerObjetivos().FirstOrDefault(x => string.Equals(x.SourcePath, SafeFull(ruta), StringComparison.OrdinalIgnoreCase));
                    if (existente == null)
                    {
                        var nuevo = new TargetInfo
                        {
                            Name = NombreLibre(repo, Texto(t, "name")),
                            SourcePath = ruta,
                            IsFolder = Numero(t, "is_folder") != 0
                        };
                        try
                        {
                            existente = repo.GuardarObjetivo(nuevo);
                            r.Objetivos++;
                        }
                        catch (RebithException ex)
                        {
                            r.Avisos.Add("Objetivo \"" + nuevo.Name + "\" (" + ruta + ") no se pudo crear: " + ex.Message);
                            continue;
                        }
                    }
                    mapaObjetivos[Numero(t, "id")] = existente;
                }

                var snaps = snapshots.OrderBy(s => Texto(s, "timestamp")).ToList();
                for (int i = 0; i < snaps.Count; i++)
                {
                    cancelar.ThrowIfCancellationRequested();
                    var s = snaps[i];
                    long idViejo = Numero(s, "id");
                    TargetInfo objetivo;
                    if (!mapaObjetivos.TryGetValue(Numero(s, "target_id"), out objetivo)) continue;
                    DateTime fecha = Tiempo.Leer(Texto(s, "timestamp")) ?? DateTime.Now;
                    string id = fecha.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-py" + idViejo.ToString(CultureInfo.InvariantCulture);
                    if (yaImportados.Contains(id)) continue;
                    if (progreso != null) progreso.Report(new ProgressInfo { Current = i + 1, Total = snaps.Count, Item = objetivo.Name + " " + Tiempo.Legible(Tiempo.Iso(fecha)), Stage = "Importando respaldos" });

                    var m = new SnapshotManifest
                    {
                        FormatVersion = Repository.FormatoActual,
                        Id = id,
                        TargetId = objetivo.Id,
                        TargetName = objetivo.Name,
                        SourcePath = objetivo.SourcePath,
                        IsFolder = objetivo.IsFolder,
                        Note = (Texto(s, "note") + " [importado de la version anterior]").Trim(),
                        User = "importado",
                        Machine = Environment.MachineName,
                        StartedLocal = Tiempo.Iso(fecha),
                        FinishedLocal = Tiempo.Iso(fecha)
                    };
                    foreach (var v in versiones[idViejo])
                    {
                        cancelar.ThrowIfCancellationRequested();
                        string hash = Texto(v, "hash");
                        string rel = Texto(v, "relative_path").Replace('\\', '/');
                        Dictionary<string, object> info;
                        if (!Hashes.EsHashValido(hash) || !archivos.TryGetValue(hash, out info))
                        {
                            m.Skipped.Add(new SkippedEntry { Path = rel, Reason = "La base anterior no tiene el contenido de este archivo." });
                            continue;
                        }
                        string problema = TraerContenido(hash, Texto(info, "archive_location"), carpetaStorage, Numero(info, "size"), r, cancelar);
                        if (problema != null)
                        {
                            m.Skipped.Add(new SkippedEntry { Path = rel, Reason = problema });
                            continue;
                        }
                        m.Files.Add(new FileEntry { Path = rel, Hash = hash, Size = Numero(info, "size"), MtimeUtcTicks = TicksDesdeUnix(Real(v, "mtime")) });
                        m.TotalBytes += Numero(info, "size");
                        r.Archivos++;
                    }
                    m.Status = m.Skipped.Count == 0 ? EstadosSnapshot.Completo : EstadosSnapshot.ConAdvertencias;
                    if (m.Files.Count == 0 && m.Skipped.Count > 0)
                    {
                        r.Avisos.Add("Respaldo del " + Tiempo.Legible(m.StartedLocal) + " de " + objetivo.Name + ": ningun archivo se pudo recuperar (" + m.Skipped[0].Reason + ")");
                        continue;
                    }
                    repo.GuardarManifiesto(m);
                    r.Respaldos++;
                }
            }

            ImportarUsuarios(lector, usuario, r);
            ImportarAuditoria(lector, r);
            servicio.Auditoria.Registrar(usuario, "importacion", string.Format("{0}: {1} objetivos, {2} respaldos, {3} archivos, {4} contenidos, {5} usuarios, {6} avisos",
                rutaDb, r.Objetivos, r.Respaldos, r.Archivos, r.Contenidos, r.Usuarios, r.Avisos.Count));
            return r;
        }

        /// <summary>Copia el .bin viejo al deposito y comprueba su huella. null = correcto.</summary>
        private string TraerContenido(string hash, string ubicacion, string carpetaStorage, long tamano, ResultadoImportacion r, CancellationToken cancelar)
        {
            ObjectStore objetos = servicio.Repo.Objetos;
            if (objetos.Existe(hash) && objetos.Verificar(hash, tamano, cancelar).Estado == EstadoObjeto.Ok) return null;

            // La ruta guardada es la de la PC donde se hizo; si la carpeta se movio, se busca junto a la base.
            var candidatos = new List<string>();
            if (!string.IsNullOrWhiteSpace(ubicacion)) candidatos.Add(ubicacion);
            candidatos.Add(Path.Combine(carpetaStorage, hash.Substring(0, 2), hash + ".bin"));
            string origen = candidatos.FirstOrDefault(c => { try { return File.Exists(c); } catch { return false; } });
            if (origen == null) return "No se encontro su archivo .bin en la carpeta storage de la version anterior.";

            string destino = objetos.RutaDe(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            string temporal = Path.Combine(servicio.Repo.CarpetaTemporal, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(origen, temporal, true);
                AtomicFile.Publicar(temporal, destino, false);
            }
            finally
            {
                AtomicFile.BorrarSinError(temporal);
            }
            ResultadoObjeto v = objetos.Verificar(hash, tamano, cancelar);
            if (v.Estado != EstadoObjeto.Ok)
            {
                objetos.Borrar(hash);
                return "El .bin de la version anterior esta danado: " + v.Detalle;
            }
            r.Contenidos++;
            return null;
        }

        private void ImportarUsuarios(SqliteLector lector, string quien, ResultadoImportacion r)
        {
            if (!lector.ExisteTabla("users")) return;
            try
            {
                string ruta = Path.Combine(servicio.Repo.CarpetaConfig, "usuarios.json");
                UserList lista = JsonFile.Leer<UserList>(ruta) ?? new UserList();
                foreach (var u in lector.LeerTabla("users"))
                {
                    string nombre = Texto(u, "username").Trim();
                    if (nombre.Length == 0 || lista.Users.Any(x => string.Equals(x.Username, nombre, StringComparison.OrdinalIgnoreCase))) continue;
                    string rol = Texto(u, "role");
                    lista.Users.Add(new UserInfo
                    {
                        Username = nombre,
                        PasswordHash = "sha256$" + Texto(u, "password_hash"),
                        Role = rol == "admin" ? Roles.Admin : rol == "supervisor" ? Roles.Supervisor : Roles.Operador,
                        IsActive = Numero(u, "is_active") != 0,
                        CanBackup = Numero(u, "can_backup") != 0,
                        CanRestore = Numero(u, "can_restore") != 0,
                        CanDelete = Numero(u, "can_manage_security") != 0,
                        CanManageUsers = Numero(u, "can_manage_users") != 0,
                        CanManageSecurity = Numero(u, "can_manage_security") != 0,
                        CreatedLocal = Tiempo.AhoraIso(),
                        LockedUntilLocal = ""
                    });
                    r.Usuarios++;
                }
                if (r.Usuarios > 0) JsonFile.Escribir(ruta, lista, true);
            }
            catch (Exception ex)
            {
                r.Avisos.Add("Usuarios no importados: " + ex.Message);
            }
        }

        private void ImportarAuditoria(SqliteLector lector, ResultadoImportacion r)
        {
            if (!lector.ExisteTabla("audit_log")) return;
            try
            {
                var usuarios = lector.ExisteTabla("users")
                    ? lector.LeerTabla("users").ToDictionary(u => Numero(u, "id"), u => Texto(u, "username"))
                    : new Dictionary<long, string>();
                foreach (var e in lector.LeerTabla("audit_log").OrderBy(x => Numero(x, "id")))
                {
                    string nombre;
                    object uid;
                    e.TryGetValue("user_id", out uid);
                    if (uid == null || !usuarios.TryGetValue(Convert.ToInt64(uid), out nombre)) nombre = "-";
                    servicio.Auditoria.Registrar(nombre, "anterior:" + Texto(e, "action"), Texto(e, "details") + " [" + Texto(e, "created_at") + "]");
                    r.EventosAuditoria++;
                }
            }
            catch (Exception ex)
            {
                r.Avisos.Add("Auditoria anterior no importada: " + ex.Message);
            }
        }

        private static string NombreLibre(Repository repo, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) nombre = "Importado";
            List<TargetInfo> todos = repo.LeerObjetivos();
            string candidato = nombre;
            int n = 2;
            while (todos.Any(t => string.Equals(t.Name, candidato, StringComparison.OrdinalIgnoreCase)))
                candidato = nombre + " (" + n++ + ")";
            return candidato;
        }

        private static string SafeFull(string ruta)
        {
            try { return Path.GetFullPath(ruta); } catch { return ruta; }
        }

        private static long TicksDesdeUnix(double segundos)
        {
            try
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks((long)(segundos * TimeSpan.TicksPerSecond)).Ticks;
            }
            catch
            {
                return DateTime.UtcNow.Ticks;
            }
        }

        private static string Texto(Dictionary<string, object> fila, string columna)
        {
            object v;
            return fila.TryGetValue(columna, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        }

        private static long Numero(Dictionary<string, object> fila, string columna)
        {
            object v;
            if (!fila.TryGetValue(columna, out v) || v == null) return 0;
            if (v is long) return (long)v;
            if (v is double) return (long)(double)v;
            long n;
            return long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        private static double Real(Dictionary<string, object> fila, string columna)
        {
            object v;
            if (!fila.TryGetValue(columna, out v) || v == null) return 0;
            if (v is double) return (double)v;
            if (v is long) return (long)v;
            double d;
            return double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
        }
    }
}
