using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rebith.Core
{
    public static class EstadosVerificacion
    {
        // Comparacion con el origen
        public const string Ok = "OK";
        public const string Modificado = "MODIFICADO";
        public const string Falta = "FALTA";
        public const string Nuevo = "NUEVO";
        public const string Ilegible = "ILEGIBLE";
        // Integridad del deposito
        public const string Danado = "DANADO";
        public const string FaltaEnDeposito = "FALTA EN DEPOSITO";
        public const string Reparado = "REPARADO";
        public const string ManifiestoDanado = "MANIFIESTO DANADO";
    }

    public class ItemVerificacion
    {
        public string Ruta = "";
        public string Estado = "";
        public string Detalle = "";
        public long Tamano;
        /// <summary>Respaldo al que pertenece (en la verificacion de integridad).</summary>
        public string Respaldo = "";
        public string Hash = "";
    }

    public class ResultadoVerificacion
    {
        public string Tipo = "";
        public string Inicio = "";
        public string Fin = "";
        public List<ItemVerificacion> Items = new List<ItemVerificacion>();
        public int ObjetosRevisados;
        public int ObjetosOmitidosPorRecientes;

        public int Cuenta(string estado)
        {
            return Items.Count(i => i.Estado == estado);
        }

        /// <summary>true = no hay nada que atender.</summary>
        public bool TodoBien
        {
            get
            {
                return Items.All(i => i.Estado == EstadosVerificacion.Ok || i.Estado == EstadosVerificacion.Reparado);
            }
        }

        public string Resumen()
        {
            var partes = Items.GroupBy(i => i.Estado).OrderBy(g => g.Key).Select(g => g.Key + ": " + g.Count());
            string texto = string.Join("   ", partes);
            return texto.Length == 0 ? "Sin elementos" : texto;
        }
    }

    /// <summary>
    /// Tres niveles de verificacion:
    ///  1. CompararConOrigen: lo que hay hoy en disco contra un respaldo (OK, MODIFICADO,
    ///     FALTA, NUEVO, ILEGIBLE). Rapida (tamano+fecha, y huella solo si difieren) o
    ///     exacta (huella de todo).
    ///  2. RevisarIntegridad: descomprime el contenido guardado y comprueba su huella
    ///     SHA-256; revisa tambien que cada manifiesto no este danado. Si hay replica,
    ///     repara solo lo danado copiando la version sana de la otra copia.
    ///  3. Automatica: RevisarIntegridad de todo el deposito cada N dias (programada).
    /// </summary>
    public class Verifier
    {
        private const int Bloque = 1024 * 1024;
        private readonly Repository repo;
        private readonly ObjectStore replica;

        public Verifier(Repository repo, ObjectStore replica)
        {
            this.repo = repo;
            this.replica = replica;
        }

        public static string HashDeArchivo(string ruta, CancellationToken cancelar)
        {
            using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, Bloque, FileOptions.SequentialScan))
            using (var sha = SHA256.Create())
            {
                byte[] buffer = new byte[Bloque];
                int leidos;
                while ((leidos = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancelar.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, leidos, null, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return Hashes.Hex(sha.Hash);
            }
        }

        // ------------------------------------------------------------------ 1. contra el origen

        public ResultadoVerificacion CompararConOrigen(SnapshotManifest m, TargetInfo objetivo, bool exacta,
            IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            var r = new ResultadoVerificacion { Tipo = exacta ? "Comparar con el origen (exacta)" : "Comparar con el origen (rapida)", Inicio = Tiempo.AhoraIso() };
            string origen = objetivo != null ? objetivo.SourcePath : m.SourcePath;
            var excluir = new Exclusiones(objetivo != null ? objetivo.Excludes : null);

            if (m.IsFolder && !Directory.Exists(origen))
                throw new RebithException("No existe la carpeta de origen: " + origen);

            var enRespaldo = new HashSet<string>(m.Files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < m.Files.Count; i++)
            {
                cancelar.ThrowIfCancellationRequested();
                FileEntry f = m.Files[i];
                if (progreso != null) progreso.Report(new ProgressInfo { Current = i + 1, Total = m.Files.Count, Item = f.Path, Stage = "Comparando" });
                string real = m.IsFolder ? Rutas.UnirSegura(origen, f.Path) : origen;
                var item = new ItemVerificacion { Ruta = f.Path, Tamano = f.Size, Hash = f.Hash, Respaldo = m.Id };
                try
                {
                    var info = new FileInfo(real);
                    if (!info.Exists)
                    {
                        item.Estado = EstadosVerificacion.Falta;
                        item.Detalle = "Ya no esta en el origen.";
                    }
                    else if (!exacta && info.Length == f.Size && Math.Abs(info.LastWriteTimeUtc.Ticks - f.MtimeUtcTicks) < TimeSpan.TicksPerSecond * 2)
                    {
                        item.Estado = EstadosVerificacion.Ok;
                    }
                    else if (info.Length != f.Size)
                    {
                        item.Estado = EstadosVerificacion.Modificado;
                        item.Detalle = "Tamano " + Tamanos.Legible(info.Length) + " (respaldo: " + Tamanos.Legible(f.Size) + "), modificado " +
                            info.LastWriteTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + ".";
                    }
                    else
                    {
                        string hash = HashDeArchivo(real, cancelar);
                        if (hash == f.Hash)
                        {
                            item.Estado = EstadosVerificacion.Ok;
                            if (!exacta) item.Detalle = "Misma huella (solo cambio la fecha).";
                        }
                        else
                        {
                            item.Estado = EstadosVerificacion.Modificado;
                            item.Detalle = "Mismo tamano, contenido distinto. Modificado " +
                                info.LastWriteTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + ".";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    item.Estado = EstadosVerificacion.Ilegible;
                    item.Detalle = ex.Message;
                }
                r.Items.Add(item);
            }

            if (m.IsFolder)
            {
                if (progreso != null) progreso.Report(new ProgressInfo { Current = 0, Total = 0, Item = "", Stage = "Buscando archivos nuevos" });
                var omitidos = new List<SkippedEntry>();
                foreach (ArchivoEncontrado a in Recorrido.Archivos(origen, excluir, omitidos, () => cancelar.IsCancellationRequested))
                {
                    if (enRespaldo.Contains(a.RutaRelativa)) continue;
                    long tamano = 0;
                    try { tamano = new FileInfo(a.RutaCompleta).Length; } catch { }
                    r.Items.Add(new ItemVerificacion { Ruta = a.RutaRelativa, Estado = EstadosVerificacion.Nuevo, Tamano = tamano, Detalle = "Existe en el origen pero no en este respaldo.", Respaldo = m.Id });
                }
                foreach (SkippedEntry s in omitidos)
                    r.Items.Add(new ItemVerificacion { Ruta = s.Path, Estado = EstadosVerificacion.Ilegible, Detalle = s.Reason, Respaldo = m.Id });
            }
            r.Fin = Tiempo.AhoraIso();
            return r;
        }

        // ------------------------------------------------------------------ 2. integridad del deposito

        /// <param name="respaldos">Respaldos a revisar (uno o todos).</param>
        /// <param name="saltarVerificadosDias">0 = revisar todo; N = saltar contenido revisado bien en los ultimos N dias.</param>
        public ResultadoVerificacion RevisarIntegridad(RepoLock candado, IList<SnapshotManifest> respaldos,
            Dictionary<string, string> manifiestosDanados, bool repararDesdeReplica, int saltarVerificadosDias,
            IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            if (candado == null) throw new ArgumentNullException("candado");
            var r = new ResultadoVerificacion { Tipo = "Integridad del deposito", Inicio = Tiempo.AhoraIso() };

            if (manifiestosDanados != null)
            {
                foreach (var par in manifiestosDanados)
                    r.Items.Add(new ItemVerificacion { Ruta = Path.GetFileName(par.Key), Estado = EstadosVerificacion.ManifiestoDanado, Detalle = par.Value });
            }

            // Cada contenido se revisa una sola vez aunque lo usen muchos respaldos.
            var porHash = new Dictionary<string, List<Tuple<SnapshotManifest, FileEntry>>>(StringComparer.Ordinal);
            foreach (SnapshotManifest m in respaldos)
            {
                foreach (FileEntry f in m.Files)
                {
                    List<Tuple<SnapshotManifest, FileEntry>> lista;
                    if (!porHash.TryGetValue(f.Hash, out lista))
                    {
                        lista = new List<Tuple<SnapshotManifest, FileEntry>>();
                        porHash[f.Hash] = lista;
                    }
                    lista.Add(Tuple.Create(m, f));
                }
            }

            VerifiedObjects verificados = repo.LeerVerificados();
            var danadosPrevios = repo.LeerDanados();
            var danadosAhora = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string h in danadosPrevios)
            {
                if (!porHash.ContainsKey(h)) danadosAhora[h] = "pendiente";
            }
            DateTime limite = DateTime.Now.AddDays(-saltarVerificadosDias);
            int n = 0;

            foreach (var par in porHash)
            {
                cancelar.ThrowIfCancellationRequested();
                n++;
                string hash = par.Key;
                FileEntry ejemplo = par.Value[0].Item2;
                if (progreso != null) progreso.Report(new ProgressInfo { Current = n, Total = porHash.Count, Item = ejemplo.Path, Stage = "Revisando contenido guardado" });

                string fechaOk;
                DateTime? ultima = verificados.LastOk.TryGetValue(hash, out fechaOk) ? Tiempo.Leer(fechaOk) : null;
                if (saltarVerificadosDias > 0 && ultima.HasValue && ultima.Value > limite && !danadosPrevios.Contains(hash) && repo.Objetos.Existe(hash))
                {
                    r.ObjetosOmitidosPorRecientes++;
                    AgregarFilas(r, par.Value, EstadosVerificacion.Ok, "Revisado bien el " + Tiempo.Legible(fechaOk) + ".");
                    continue;
                }

                r.ObjetosRevisados++;
                ResultadoObjeto resultado = repo.Objetos.Verificar(hash, ejemplo.Size, cancelar);
                if (resultado.Estado == EstadoObjeto.Ok)
                {
                    verificados.LastOk[hash] = Tiempo.AhoraIso();
                    AgregarFilas(r, par.Value, EstadosVerificacion.Ok, "");
                    CuidarReplica(hash, ejemplo.Size, repararDesdeReplica, cancelar);
                    continue;
                }

                string estado = resultado.Estado == EstadoObjeto.Falta ? EstadosVerificacion.FaltaEnDeposito : EstadosVerificacion.Danado;
                string detalle = resultado.Detalle;
                if (repararDesdeReplica && replica != null)
                {
                    string reparacion = RepararDesdeReplica(hash, ejemplo.Size, cancelar);
                    if (reparacion == null)
                    {
                        verificados.LastOk[hash] = Tiempo.AhoraIso();
                        AgregarFilas(r, par.Value, EstadosVerificacion.Reparado, detalle + " Se reparo con la copia sana de la replica.");
                        continue;
                    }
                    detalle += " La replica tampoco sirve: " + reparacion;
                }
                verificados.LastOk.Remove(hash);
                danadosAhora[hash] = detalle;
                AgregarFilas(r, par.Value, estado, detalle + " Se reparara solo en el siguiente respaldo si el archivo original sigue igual.");
            }

            try
            {
                repo.GuardarVerificados(verificados);
                repo.GuardarDanados(danadosAhora);
            }
            catch (Exception ex)
            {
                Log.Advertencia("No se pudo guardar el estado de verificacion: " + ex.Message);
            }
            r.Fin = Tiempo.AhoraIso();
            return r;
        }

        private static void AgregarFilas(ResultadoVerificacion r, List<Tuple<SnapshotManifest, FileEntry>> usos, string estado, string detalle)
        {
            foreach (var uso in usos)
            {
                r.Items.Add(new ItemVerificacion
                {
                    Ruta = uso.Item2.Path,
                    Estado = estado,
                    Detalle = detalle,
                    Tamano = uso.Item2.Size,
                    Respaldo = uso.Item1.Id,
                    Hash = uso.Item2.Hash
                });
            }
        }

        /// <summary>Si la replica no tiene este contenido (o lo tiene danado), se lo copia. Sin excepciones.</summary>
        private void CuidarReplica(string hash, long tamano, bool reparar, CancellationToken cancelar)
        {
            if (!reparar || replica == null) return;
            try
            {
                if (replica.Verificar(hash, tamano, cancelar).Estado != EstadoObjeto.Ok)
                    repo.Objetos.CopiarA(replica, hash, cancelar);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Advertencia("No se pudo actualizar la replica para " + hash + ": " + ex.Message);
            }
        }

        /// <summary>null = reparado. Si no, el motivo.</summary>
        private string RepararDesdeReplica(string hash, long tamano, CancellationToken cancelar)
        {
            try
            {
                ResultadoObjeto enReplica = replica.Verificar(hash, tamano, cancelar);
                if (enReplica.Estado != EstadoObjeto.Ok) return enReplica.Detalle;
                if (!replica.CopiarA(repo.Objetos, hash, cancelar)) return "la copia reparada no quedo sana.";
                Log.Info("Contenido " + hash + " reparado desde la replica.");
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ------------------------------------------------------------------ reportes

        /// <summary>Guarda el resultado como CSV (se abre en Excel). Devuelve la ruta.</summary>
        public static string GuardarReporte(ResultadoVerificacion r, string carpeta, string nombreBase)
        {
            Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(carpeta, nombreBase + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            var sb = new StringBuilder();
            sb.AppendLine("Tipo," + Csv(r.Tipo));
            sb.AppendLine("Inicio," + Csv(Tiempo.Legible(r.Inicio)) + ",Fin," + Csv(Tiempo.Legible(r.Fin)));
            sb.AppendLine("Resumen," + Csv(r.Resumen()));
            sb.AppendLine();
            sb.AppendLine("Estado,Archivo,Tamano (bytes),Respaldo,Detalle,Huella");
            foreach (ItemVerificacion i in r.Items.OrderBy(x => x.Estado == EstadosVerificacion.Ok ? 1 : 0).ThenBy(x => x.Ruta, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine(string.Join(",", Csv(i.Estado), Csv(i.Ruta), i.Tamano.ToString(CultureInfo.InvariantCulture), Csv(i.Respaldo), Csv(i.Detalle), Csv(i.Hash)));
            }
            AtomicFile.WriteAllBytes(ruta, new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(sb.ToString())).ToArray(), false);
            return ruta;
        }

        private static string Csv(string texto)
        {
            texto = texto ?? "";
            if (texto.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + texto.Replace("\"", "\"\"") + "\"";
            return texto;
        }
    }
}
