using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Rebith.Core
{
    public class ResultadoRespaldo
    {
        public SnapshotManifest Manifiesto;
        public int Reutilizados;
        public int Reparados;
        public TimeSpan Duracion;
    }

    /// <summary>
    /// Hace un respaldo de un objetivo. Reglas:
    ///  - Un archivo que no se puede leer (en uso, sin permiso, cambiando) NO detiene el
    ///    respaldo: se anota en "omitidos" y el respaldo queda "Con advertencias".
    ///  - Un problema del deposito (disco lleno, red caida) SI lo detiene, y no queda
    ///    ningun respaldo a medias registrado.
    ///  - El manifiesto se escribe al final, de una sola vez: o existe completo o no existe.
    ///  - Todo contenido nuevo se relee y se comprueba antes de dar el respaldo por bueno.
    /// </summary>
    public class BackupEngine
    {
        private const int IntentosPorArchivo = 3;
        private readonly Repository repo;

        public BackupEngine(Repository repo)
        {
            this.repo = repo;
        }

        public ResultadoRespaldo Respaldar(RepoLock candado, TargetInfo objetivo, string nota, string usuario,
            IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            if (candado == null) throw new ArgumentNullException("candado");
            var reloj = Stopwatch.StartNew();
            string origen = objetivo.SourcePath;
            bool esCarpeta = Directory.Exists(origen);
            bool esArchivo = File.Exists(origen);
            if (!esCarpeta && !esArchivo)
                throw new RebithException("No existe la ruta a respaldar: " + origen);
            if (esCarpeta != objetivo.IsFolder)
                throw new RebithException("La ruta " + origen + (esCarpeta ? " ahora es una carpeta" : " ahora es un archivo") + " y el objetivo esperaba lo contrario. Revise el objetivo.");
            RevisarEspacio();

            var m = new SnapshotManifest
            {
                FormatVersion = Repository.FormatoActual,
                Id = Repository.NuevoIdRespaldo(),
                TargetId = objetivo.Id,
                TargetName = objetivo.Name,
                SourcePath = origen,
                IsFolder = esCarpeta,
                Note = nota ?? "",
                User = usuario ?? "",
                Machine = Environment.MachineName,
                StartedLocal = Tiempo.AhoraIso()
            };
            repo.MarcarEnCurso(new InProgressMarker
            {
                SnapshotId = m.Id,
                TargetId = objetivo.Id,
                StartedLocal = m.StartedLocal,
                Machine = Environment.MachineName,
                ProcessId = Process.GetCurrentProcess().Id
            });

            var resultado = new ResultadoRespaldo { Manifiesto = m };
            try
            {
                Reportar(progreso, 0, 0, "", "Buscando archivos...");
                List<ArchivoEncontrado> archivos;
                if (esCarpeta)
                {
                    archivos = Recorrido.Archivos(origen, new Exclusiones(objetivo.Excludes), m.Skipped,
                        () => cancelar.IsCancellationRequested);
                }
                else
                {
                    archivos = new List<ArchivoEncontrado> { new ArchivoEncontrado { RutaCompleta = origen, RutaRelativa = Path.GetFileName(origen) } };
                }

                Dictionary<string, FileEntry> anterior = UltimoRespaldoCompleto(objetivo.Id);
                HashSet<string> danados = repo.LeerDanados();
                var nuevos = new Dictionary<string, long>(StringComparer.Ordinal);

                for (int i = 0; i < archivos.Count; i++)
                {
                    cancelar.ThrowIfCancellationRequested();
                    ArchivoEncontrado a = archivos[i];
                    Reportar(progreso, i + 1, archivos.Count, a.RutaRelativa, "Respaldando");

                    FileEntry previo;
                    FileEntry reutilizado = null;
                    if (!objetivo.AlwaysReadAll && anterior.TryGetValue(a.RutaRelativa, out previo))
                        reutilizado = IntentarReutilizar(a.RutaCompleta, previo, danados);
                    if (reutilizado != null)
                    {
                        m.Files.Add(reutilizado);
                        m.TotalBytes += reutilizado.Size;
                        resultado.Reutilizados++;
                        continue;
                    }

                    string motivo;
                    FileEntry entrada = LeerYGuardar(a, danados, cancelar, out motivo, resultado, nuevos);
                    if (entrada == null)
                    {
                        m.Skipped.Add(new SkippedEntry { Path = a.RutaRelativa, Reason = motivo });
                        continue;
                    }
                    m.Files.Add(entrada);
                    m.TotalBytes += entrada.Size;
                }

                ComprobarContenidoNuevo(m, nuevos, progreso, cancelar);

                if (m.Files.Count == 0 && m.Skipped.Count > 0)
                    throw new RebithException("No se pudo respaldar ningun archivo (" + m.Skipped.Count + " con problemas). Primer motivo: " + m.Skipped[0].Path + ": " + m.Skipped[0].Reason);

                m.NewObjects = nuevos.Count;
                m.NewBytesStored = nuevos.Values.Sum();
                m.Status = m.Skipped.Count == 0 ? EstadosSnapshot.Completo : EstadosSnapshot.ConAdvertencias;
                m.FinishedLocal = Tiempo.AhoraIso();
                repo.GuardarManifiesto(m);

                if (resultado.Reparados > 0) QuitarDeDanados(m.Files.Select(f => f.Hash));
                resultado.Duracion = reloj.Elapsed;
                Log.Info(string.Format("Respaldo {0} de \"{1}\": {2} archivos, {3} omitidos, {4} nuevos, {5} reutilizados, {6:0.0} s",
                    m.Id, objetivo.Name, m.Files.Count, m.Skipped.Count, m.NewObjects, resultado.Reutilizados, resultado.Duracion.TotalSeconds));
                return resultado;
            }
            finally
            {
                repo.QuitarMarcaEnCurso(m.Id);
            }
        }

        private void RevisarEspacio()
        {
            RepoSettings ajustes = repo.LeerAjustes();
            long libre = Rutas.EspacioLibre(repo.Raiz);
            if (libre >= 0 && libre < (long)ajustes.MinFreeSpaceMB * 1024 * 1024)
                throw new RebithException("Queda poco espacio en el disco del deposito (" + Tamanos.Legible(libre) +
                    "). Libere espacio o cambie el minimo en Configuracion. No se inicio el respaldo.");
        }

        private Dictionary<string, FileEntry> UltimoRespaldoCompleto(string targetId)
        {
            var d = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            SnapshotManifest ultimo = repo.RespaldosDe(targetId).FirstOrDefault();
            if (ultimo == null) return d;
            foreach (FileEntry f in ultimo.Files) d[f.Path] = f;
            return d;
        }

        /// <summary>
        /// Si el archivo tiene el mismo tamano y fecha que en el respaldo anterior, y ese
        /// contenido sigue sano en el deposito, no hace falta volver a leerlo.
        /// </summary>
        private FileEntry IntentarReutilizar(string ruta, FileEntry previo, HashSet<string> danados)
        {
            try
            {
                var info = new FileInfo(ruta);
                if (!info.Exists) return null;
                if (info.Length != previo.Size || info.LastWriteTimeUtc.Ticks != previo.MtimeUtcTicks) return null;
                if (danados.Contains(previo.Hash) || !repo.Objetos.Existe(previo.Hash)) return null;
                return new FileEntry { Path = previo.Path, Hash = previo.Hash, Size = previo.Size, MtimeUtcTicks = previo.MtimeUtcTicks };
            }
            catch
            {
                return null;
            }
        }

        private FileEntry LeerYGuardar(ArchivoEncontrado a, HashSet<string> danados, CancellationToken cancelar,
            out string motivo, ResultadoRespaldo resultado, Dictionary<string, long> nuevos)
        {
            motivo = "";
            for (int intento = 1; intento <= IntentosPorArchivo; intento++)
            {
                cancelar.ThrowIfCancellationRequested();
                try
                {
                    var antes = new FileInfo(a.RutaCompleta);
                    if (!antes.Exists)
                    {
                        motivo = "El archivo desaparecio durante el respaldo.";
                        return null;
                    }
                    long tamanoAntes = antes.Length;
                    long fechaAntes = antes.LastWriteTimeUtc.Ticks;

                    ObjetoGuardado obj = repo.Objetos.GuardarDesdeArchivo(a.RutaCompleta, danados, cancelar);

                    var despues = new FileInfo(a.RutaCompleta);
                    if (despues.Exists && despues.Length == tamanoAntes && despues.LastWriteTimeUtc.Ticks == fechaAntes && obj.Tamano == tamanoAntes)
                    {
                        if (obj.EsNuevo) nuevos[obj.Hash] = obj.BytesGuardados;
                        if (obj.Reparado) resultado.Reparados++;
                        return new FileEntry { Path = a.RutaRelativa, Hash = obj.Hash, Size = obj.Tamano, MtimeUtcTicks = fechaAntes };
                    }
                    motivo = "El archivo cambio mientras se leia (otro programa lo esta escribiendo).";
                    if (obj.EsNuevo && !nuevos.ContainsKey(obj.Hash)) repo.Objetos.Borrar(obj.Hash);
                }
                catch (SourceFileException ex)
                {
                    motivo = DescribirError(ex.InnerException ?? ex);
                }
                if (intento < IntentosPorArchivo)
                {
                    if (cancelar.WaitHandle.WaitOne(500 * intento)) cancelar.ThrowIfCancellationRequested();
                }
            }
            return null;
        }

        private static string DescribirError(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "Sin permiso para leerlo: " + ex.Message;
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException) return "El archivo desaparecio durante el respaldo.";
            if (ex is PathTooLongException) return "La ruta es demasiado larga: " + ex.Message;
            if (ex is IOException) return "En uso o bloqueado por otro programa: " + ex.Message;
            return ex.GetType().Name + ": " + ex.Message;
        }

        /// <summary>
        /// Relee cada contenido escrito en este respaldo y comprueba su huella. Si alguno
        /// salio mal (disco o red con fallas) se detiene: no se registra un respaldo que
        /// no se podria restaurar.
        /// </summary>
        private void ComprobarContenidoNuevo(SnapshotManifest m, Dictionary<string, long> nuevos,
            IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            if (nuevos.Count == 0) return;
            var tamanos = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (FileEntry f in m.Files) tamanos[f.Hash] = f.Size;
            int n = 0;
            var verificados = repo.LeerVerificados();
            foreach (string hash in nuevos.Keys)
            {
                cancelar.ThrowIfCancellationRequested();
                n++;
                Reportar(progreso, n, nuevos.Count, hash.Substring(0, 12), "Comprobando lo guardado");
                long esperado;
                if (!tamanos.TryGetValue(hash, out esperado)) esperado = -1;
                ResultadoObjeto r = repo.Objetos.Verificar(hash, esperado, cancelar);
                if (r.Estado != EstadoObjeto.Ok)
                {
                    repo.Objetos.Borrar(hash);
                    throw new RebithException("El deposito no guardo bien un archivo (" + r.Detalle +
                        "). Revise el disco o la red del deposito. El respaldo no se registro.");
                }
                verificados.LastOk[hash] = Tiempo.AhoraIso();
            }
            try
            {
                repo.GuardarVerificados(verificados);
            }
            catch (Exception ex)
            {
                Log.Advertencia("No se pudo guardar verificados.json: " + ex.Message);
            }
        }

        private void QuitarDeDanados(IEnumerable<string> hashes)
        {
            try
            {
                HashSet<string> danados = repo.LeerDanados();
                var actuales = new HashSet<string>(hashes);
                var quedan = danados.Where(h => !actuales.Contains(h) || !repo.Objetos.Existe(h)).ToDictionary(h => h, h => "pendiente");
                repo.GuardarDanados(quedan);
            }
            catch (Exception ex)
            {
                Log.Advertencia("No se pudo actualizar danados.json: " + ex.Message);
            }
        }

        private static void Reportar(IProgress<ProgressInfo> progreso, int actual, int total, string item, string etapa)
        {
            if (progreso == null) return;
            progreso.Report(new ProgressInfo { Current = actual, Total = total, Item = item, Stage = etapa });
        }
    }
}
