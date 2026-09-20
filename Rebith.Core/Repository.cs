using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Rebith.Core
{
    /// <summary>
    /// El deposito es una carpeta autocontenida:
    ///   deposito.json          identidad y version del formato
    ///   objetos\ab\hash.bin    contenido comprimido y deduplicado
    ///   respaldos\id.json      manifiesto de cada respaldo (lista completa de archivos)
    ///   config\                objetivos, usuarios, ajustes, estado de verificacion
    ///   auditoria\             bitacora encadenada de acciones
    ///   reportes\              reportes de verificacion (CSV)
    ///   temporal\              archivos a medio escribir (se limpian solos)
    /// No hay base de datos: los manifiestos SON el indice. Si se pierde la carpeta
    /// config, los respaldos se siguen pudiendo leer y restaurar.
    /// </summary>
    public class Repository
    {
        public const int FormatoActual = 1;
        private readonly object candadoConfig = new object();
        private readonly Dictionary<string, CacheManifiesto> cache = new Dictionary<string, CacheManifiesto>(StringComparer.OrdinalIgnoreCase);

        public string Raiz { get; private set; }
        public ObjectStore Objetos { get; private set; }
        public string CarpetaRespaldos { get { return Path.Combine(Raiz, "respaldos"); } }
        public string CarpetaConfig { get { return Path.Combine(Raiz, "config"); } }
        public string CarpetaAuditoria { get { return Path.Combine(Raiz, "auditoria"); } }
        public string CarpetaReportes { get { return Path.Combine(Raiz, "reportes"); } }
        public string CarpetaTemporal { get { return Path.Combine(Raiz, "temporal"); } }
        public RepoInfo Info { get; private set; }

        private class CacheManifiesto
        {
            public DateTime Fecha;
            public long Tamano;
            public SnapshotManifest Manifiesto;
        }

        private Repository(string raiz)
        {
            Raiz = Path.GetFullPath(raiz);
        }

        /// <summary>Abre el deposito. Si la carpeta esta vacia o no existe, lo crea.</summary>
        public static Repository AbrirOCrear(string raiz)
        {
            if (string.IsNullOrWhiteSpace(raiz)) throw new RebithException("No se indico la carpeta del deposito.");
            var repo = new Repository(raiz);
            try
            {
                Directory.CreateDirectory(repo.Raiz);
                string rutaInfo = Path.Combine(repo.Raiz, "deposito.json");
                RepoInfo info = JsonFile.Leer<RepoInfo>(rutaInfo);
                if (info == null)
                {
                    bool vacia = !Directory.EnumerateFileSystemEntries(repo.Raiz).Any();
                    if (!vacia && !Directory.Exists(Path.Combine(repo.Raiz, "objetos")))
                        throw new RebithException("La carpeta " + repo.Raiz + " no esta vacia y no es un deposito de Rebith. Elija una carpeta vacia.");
                    info = new RepoInfo
                    {
                        FormatVersion = FormatoActual,
                        RepoId = Guid.NewGuid().ToString("N"),
                        CreatedLocal = Tiempo.AhoraIso(),
                        CreatedBy = Environment.MachineName
                    };
                    JsonFile.Escribir(rutaInfo, info, true);
                }
                if (info.FormatVersion > FormatoActual)
                    throw new RebithException("Este deposito fue creado con una version mas nueva de Rebith (formato " + info.FormatVersion + "). Actualice el programa.");
                repo.Info = info;
                foreach (string carpeta in new[] { repo.CarpetaRespaldos, repo.CarpetaConfig, repo.CarpetaAuditoria, repo.CarpetaReportes, repo.CarpetaTemporal })
                    Directory.CreateDirectory(carpeta);
                repo.Objetos = new ObjectStore(Path.Combine(repo.Raiz, "objetos"), repo.CarpetaTemporal);
                return repo;
            }
            catch (RebithException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RebithException("No se pudo abrir el deposito en " + repo.Raiz + ": " + ex.Message, ex);
            }
        }

        public RepoLock TomarCandado(string operacion, string usuario)
        {
            RepoLock candado = RepoLock.Tomar(Raiz, operacion, usuario);
            try
            {
                LimpiarRestosDeOperacionesCaidas();
            }
            catch (Exception ex)
            {
                Log.Error("Limpieza de restos", ex);
            }
            return candado;
        }

        // ------------------------------------------------------------------ objetivos

        private string RutaObjetivos { get { return Path.Combine(CarpetaConfig, "objetivos.json"); } }

        public List<TargetInfo> LeerObjetivos()
        {
            lock (candadoConfig)
            {
                TargetList lista = JsonFile.Leer<TargetList>(RutaObjetivos);
                return lista == null ? new List<TargetInfo>() : lista.Targets;
            }
        }

        public TargetInfo BuscarObjetivo(string id)
        {
            return LeerObjetivos().FirstOrDefault(t => t.Id == id);
        }

        /// <summary>Crea o actualiza un objetivo. Valida ruta, nombre y que no se repita.</summary>
        public TargetInfo GuardarObjetivo(TargetInfo objetivo)
        {
            if (objetivo == null) throw new ArgumentNullException("objetivo");
            objetivo.Name = (objetivo.Name ?? "").Trim();
            objetivo.SourcePath = (objetivo.SourcePath ?? "").Trim();
            if (objetivo.Name.Length == 0) throw new RebithException("Escriba un nombre para el objetivo.");
            if (objetivo.SourcePath.Length == 0) throw new RebithException("Elija la carpeta o el archivo a respaldar.");
            try
            {
                objetivo.SourcePath = Path.GetFullPath(objetivo.SourcePath);
            }
            catch (Exception ex)
            {
                throw new RebithException("La ruta no es valida: " + ex.Message);
            }
            if (Rutas.SeTraslapan(objetivo.SourcePath, Raiz))
                throw new RebithException("La carpeta a respaldar no puede contener al deposito ni estar dentro de el.");
            if (objetivo.Schedule == null) objetivo.Schedule = new ScheduleInfo();
            objetivo.Schedule.Hour = Math.Max(0, Math.Min(23, objetivo.Schedule.Hour));
            objetivo.Schedule.Minute = Math.Max(0, Math.Min(59, objetivo.Schedule.Minute));
            objetivo.Schedule.DayOfWeek = Math.Max(0, Math.Min(6, objetivo.Schedule.DayOfWeek));
            objetivo.KeepLast = Math.Max(0, objetivo.KeepLast);
            objetivo.Excludes = (objetivo.Excludes ?? new List<string>()).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct().ToList();

            lock (candadoConfig)
            {
                List<TargetInfo> lista = LeerObjetivos();
                if (lista.Any(t => t.Id != objetivo.Id && string.Equals(t.Name, objetivo.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new RebithException("Ya existe un objetivo con el nombre \"" + objetivo.Name + "\".");
                TargetInfo repetido = lista.FirstOrDefault(t => t.Id != objetivo.Id && string.Equals(t.SourcePath, objetivo.SourcePath, StringComparison.OrdinalIgnoreCase));
                if (repetido != null)
                    throw new RebithException("Esa ruta ya se respalda en el objetivo \"" + repetido.Name + "\".");

                if (string.IsNullOrEmpty(objetivo.Id))
                {
                    objetivo.Id = Guid.NewGuid().ToString("N").Substring(0, 12);
                    objetivo.CreatedLocal = Tiempo.AhoraIso();
                    lista.Add(objetivo);
                }
                else
                {
                    int i = lista.FindIndex(t => t.Id == objetivo.Id);
                    if (i < 0) lista.Add(objetivo); else lista[i] = objetivo;
                }
                JsonFile.Escribir(RutaObjetivos, new TargetList { Targets = lista }, true);
                return objetivo;
            }
        }

        /// <summary>Quita el objetivo de la lista. Sus respaldos NO se borran (siguen en el historial).</summary>
        public void QuitarObjetivo(string id)
        {
            lock (candadoConfig)
            {
                List<TargetInfo> lista = LeerObjetivos();
                lista.RemoveAll(t => t.Id == id);
                JsonFile.Escribir(RutaObjetivos, new TargetList { Targets = lista }, true);
            }
        }

        /// <summary>Guarda solo el resultado del programador sin pisar otros cambios del objetivo.</summary>
        public void AnotarEjecucionProgramada(string id, string resultado)
        {
            lock (candadoConfig)
            {
                List<TargetInfo> lista = LeerObjetivos();
                TargetInfo t = lista.FirstOrDefault(x => x.Id == id);
                if (t == null) return;
                t.Schedule.LastRunLocal = Tiempo.AhoraIso();
                t.Schedule.LastResult = resultado ?? "";
                t.Schedule.RetryAfterLocal = "";
                JsonFile.Escribir(RutaObjetivos, new TargetList { Targets = lista }, true);
            }
        }

        /// <summary>El programado fallo: se reintenta en una hora sin perder el turno.</summary>
        public void AnotarFalloProgramado(string id, string error)
        {
            lock (candadoConfig)
            {
                List<TargetInfo> lista = LeerObjetivos();
                TargetInfo t = lista.FirstOrDefault(x => x.Id == id);
                if (t == null) return;
                t.Schedule.LastResult = "ERROR " + Tiempo.Legible(Tiempo.AhoraIso()) + ": " + error;
                t.Schedule.RetryAfterLocal = Tiempo.Iso(DateTime.Now.AddHours(1));
                JsonFile.Escribir(RutaObjetivos, new TargetList { Targets = lista }, true);
            }
        }

        // ------------------------------------------------------------------ ajustes

        private string RutaAjustes { get { return Path.Combine(CarpetaConfig, "ajustes.json"); } }

        public RepoSettings LeerAjustes()
        {
            lock (candadoConfig)
            {
                return JsonFile.Leer<RepoSettings>(RutaAjustes) ?? new RepoSettings();
            }
        }

        public void GuardarAjustes(RepoSettings ajustes)
        {
            lock (candadoConfig)
            {
                ajustes.AutoVerifyDays = Math.Max(0, ajustes.AutoVerifyDays);
                ajustes.MinFreeSpaceMB = Math.Max(0, ajustes.MinFreeSpaceMB);
                ajustes.SessionTimeoutMinutes = Math.Max(1, ajustes.SessionTimeoutMinutes);
                JsonFile.Escribir(RutaAjustes, ajustes, true);
            }
        }

        // ------------------------------------------------------------------ manifiestos

        public static string NuevoIdRespaldo()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        public static string CalcularHuellaContenido(SnapshotManifest m)
        {
            var sb = new StringBuilder();
            sb.Append(m.Id).Append('\n').Append(m.TargetId).Append('\n');
            foreach (FileEntry f in m.Files)
            {
                sb.Append(f.Path).Append('|').Append(f.Hash).Append('|')
                  .Append(f.Size.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(f.MtimeUtcTicks.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return Hashes.Sha256Texto(sb.ToString());
        }

        private string RutaManifiesto(string id)
        {
            foreach (char c in id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) throw new RebithException("Id de respaldo invalido: " + id);
            }
            return Path.Combine(CarpetaRespaldos, id + ".json");
        }

        public void GuardarManifiesto(SnapshotManifest m)
        {
            m.ContentHash = CalcularHuellaContenido(m);
            JsonFile.Escribir(RutaManifiesto(m.Id), m, false);
        }

        public class LecturaManifiestos
        {
            public List<SnapshotManifest> Manifiestos = new List<SnapshotManifest>();
            /// <summary>Archivo -> motivo. Manifiestos que no se pudieron leer o estan alterados.</summary>
            public Dictionary<string, string> Danados = new Dictionary<string, string>();
        }

        /// <summary>Lee todos los manifiestos (con cache). Nunca falla por uno danado: lo reporta aparte.</summary>
        public LecturaManifiestos LeerManifiestos()
        {
            var r = new LecturaManifiestos();
            string[] archivos;
            try
            {
                archivos = Directory.GetFiles(CarpetaRespaldos, "*.json");
            }
            catch (Exception ex)
            {
                throw new RebithException("No se pudo leer la carpeta de respaldos: " + ex.Message, ex);
            }
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string archivo in archivos)
            {
                vistos.Add(archivo);
                try
                {
                    var info = new FileInfo(archivo);
                    CacheManifiesto c;
                    lock (cache)
                    {
                        if (cache.TryGetValue(archivo, out c) && c.Fecha == info.LastWriteTimeUtc && c.Tamano == info.Length)
                        {
                            r.Manifiestos.Add(c.Manifiesto);
                            continue;
                        }
                    }
                    SnapshotManifest m = JsonFile.Deserializar<SnapshotManifest>(JsonFile.LeerTextoCompartido(archivo));
                    string problema = RevisarManifiesto(m, Path.GetFileNameWithoutExtension(archivo));
                    if (problema != null)
                    {
                        r.Danados[archivo] = problema;
                        continue;
                    }
                    lock (cache) cache[archivo] = new CacheManifiesto { Fecha = info.LastWriteTimeUtc, Tamano = info.Length, Manifiesto = m };
                    r.Manifiestos.Add(m);
                }
                catch (Exception ex)
                {
                    r.Danados[archivo] = "No se puede leer: " + ex.Message;
                }
            }
            lock (cache)
            {
                foreach (string viejo in cache.Keys.Where(k => !vistos.Contains(k)).ToList()) cache.Remove(viejo);
            }
            r.Manifiestos.Sort((a, b) => string.CompareOrdinal(b.Id, a.Id));
            return r;
        }

        private static string RevisarManifiesto(SnapshotManifest m, string nombreArchivo)
        {
            if (m == null) return "Archivo vacio.";
            if (m.FormatVersion > FormatoActual) return "Formato mas nuevo que este programa.";
            if (!string.Equals(m.Id, nombreArchivo, StringComparison.OrdinalIgnoreCase)) return "El nombre del archivo no coincide con su id.";
            if (!string.IsNullOrEmpty(m.ContentHash) && m.ContentHash != CalcularHuellaContenido(m))
                return "La lista de archivos fue alterada o se dano (la huella no coincide).";
            foreach (FileEntry f in m.Files)
            {
                if (!Hashes.EsHashValido(f.Hash)) return "Hash invalido en " + f.Path;
            }
            return null;
        }

        public List<SnapshotManifest> RespaldosDe(string targetId)
        {
            return LeerManifiestos().Manifiestos.Where(m => m.TargetId == targetId).ToList();
        }

        public SnapshotManifest BuscarRespaldo(string id)
        {
            return LeerManifiestos().Manifiestos.FirstOrDefault(m => m.Id == id);
        }

        /// <summary>Borra el manifiesto. El contenido que ya nadie use lo quita LimpiarObjetos.</summary>
        public void BorrarManifiesto(string id)
        {
            string ruta = RutaManifiesto(id);
            if (!File.Exists(ruta)) return;
            AtomicFile.QuitarSoloLectura(ruta);
            File.Delete(ruta);
            lock (cache) cache.Remove(ruta);
        }

        // ------------------------------------------------------------------ en curso

        public void MarcarEnCurso(InProgressMarker marca)
        {
            JsonFile.Escribir(Path.Combine(CarpetaRespaldos, marca.SnapshotId + ".encurso"), marca, false);
        }

        public void QuitarMarcaEnCurso(string snapshotId)
        {
            AtomicFile.BorrarSinError(Path.Combine(CarpetaRespaldos, snapshotId + ".encurso"));
        }

        /// <summary>
        /// Se llama con el candado tomado. Cualquier marca "en curso" o temporal que exista
        /// es de una operacion que se cayo (corte de luz, proceso terminado).
        /// </summary>
        private void LimpiarRestosDeOperacionesCaidas()
        {
            foreach (string marca in Directory.GetFiles(CarpetaRespaldos, "*.encurso"))
            {
                Log.Advertencia("Respaldo que no termino (se cayo el proceso): " + Path.GetFileName(marca) + ". Se descarta.");
                AtomicFile.BorrarSinError(marca);
            }
            foreach (string tmp in Directory.GetFiles(CarpetaTemporal))
                AtomicFile.BorrarSinError(tmp);
            foreach (string tmp in Directory.GetFiles(CarpetaRespaldos, ".*.tmp"))
                AtomicFile.BorrarSinError(tmp);
        }

        // ------------------------------------------------------------------ limpieza

        public class ResultadoLimpieza
        {
            public int ObjetosBorrados;
            public long BytesLiberados;
            public bool Cancelada;
            public string Motivo = "";
        }

        /// <summary>
        /// Borra el contenido que ya no usa ningun respaldo. REGLA DE SEGURIDAD: si hay
        /// un solo manifiesto que no se pudo leer, NO borra nada, porque no se puede saber
        /// que contenido necesita. Requiere el candado tomado.
        /// </summary>
        public ResultadoLimpieza LimpiarObjetos(RepoLock candado, CancellationToken cancelar)
        {
            if (candado == null) throw new ArgumentNullException("candado");
            var r = new ResultadoLimpieza();
            LecturaManifiestos lectura = LeerManifiestos();
            if (lectura.Danados.Count > 0)
            {
                r.Cancelada = true;
                r.Motivo = "Hay " + lectura.Danados.Count + " manifiesto(s) que no se pueden leer; por seguridad no se borra contenido.";
                Log.Advertencia("Limpieza cancelada: " + r.Motivo);
                return r;
            }
            var usados = new HashSet<string>(lectura.Manifiestos.SelectMany(m => m.Files.Select(f => f.Hash)), StringComparer.Ordinal);
            DateTime limite = DateTime.UtcNow.AddHours(-1);
            foreach (string sub in Directory.GetDirectories(Objetos.Raiz))
            {
                foreach (string archivo in Directory.GetFiles(sub, "*.bin"))
                {
                    cancelar.ThrowIfCancellationRequested();
                    string hash = Path.GetFileNameWithoutExtension(archivo);
                    if (usados.Contains(hash)) continue;
                    try
                    {
                        var info = new FileInfo(archivo);
                        if (info.LastWriteTimeUtc > limite) continue;
                        long tamano = info.Length;
                        AtomicFile.QuitarSoloLectura(archivo);
                        File.Delete(archivo);
                        r.ObjetosBorrados++;
                        r.BytesLiberados += tamano;
                    }
                    catch (Exception ex)
                    {
                        Log.Advertencia("No se pudo borrar " + archivo + ": " + ex.Message);
                    }
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ estado de verificacion

        private string RutaVerificados { get { return Path.Combine(CarpetaConfig, "verificados.json"); } }
        private string RutaDanados { get { return Path.Combine(CarpetaConfig, "danados.json"); } }

        public VerifiedObjects LeerVerificados()
        {
            try
            {
                return JsonFile.Leer<VerifiedObjects>(RutaVerificados) ?? new VerifiedObjects();
            }
            catch (Exception ex)
            {
                Log.Advertencia("verificados.json no se pudo leer, se empieza de cero: " + ex.Message);
                return new VerifiedObjects();
            }
        }

        public void GuardarVerificados(VerifiedObjects v)
        {
            JsonFile.Escribir(RutaVerificados, v, true);
        }

        /// <summary>Contenido que la verificacion encontro danado o faltante (para curarlo en el siguiente respaldo).</summary>
        public HashSet<string> LeerDanados()
        {
            try
            {
                VerifiedObjects d = JsonFile.Leer<VerifiedObjects>(RutaDanados);
                return d == null ? new HashSet<string>() : new HashSet<string>(d.LastOk.Keys);
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        public void GuardarDanados(IDictionary<string, string> hashConMotivo)
        {
            var d = new VerifiedObjects();
            foreach (var par in hashConMotivo) d.LastOk[par.Key] = par.Value;
            JsonFile.Escribir(RutaDanados, d, true);
        }

        public long TamanoObjetos()
        {
            long total = 0;
            try
            {
                foreach (string archivo in Directory.EnumerateFiles(Objetos.Raiz, "*.bin", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(archivo).Length; } catch { }
                }
            }
            catch
            {
            }
            return total;
        }
    }
}
