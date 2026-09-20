using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Rebith.Core
{
    public class FalloRestauracion
    {
        public string Ruta = "";
        public string Motivo = "";
    }

    public class ResultadoRestauracion
    {
        public int Restaurados;
        public int SinCambios;
        public List<FalloRestauracion> Fallos = new List<FalloRestauracion>();
        public string Destino = "";
        /// <summary>Respaldo automatico de lo que habia antes de restaurar encima (si aplica).</summary>
        public string RespaldoPrevioId = "";
    }

    /// <summary>
    /// Restaura archivos de un respaldo. Cada archivo:
    ///  1. se descomprime a un temporal junto al destino,
    ///  2. se comprueba su huella SHA-256 (si no coincide, NO se toca el destino),
    ///  3. se pone en su lugar de un solo movimiento y se le devuelve su fecha original.
    /// Un archivo que falla (en uso, sin permiso) se anota y se sigue con los demas.
    /// Si el contenido principal esta danado y hay replica, se usa la replica.
    /// </summary>
    public class RestoreEngine
    {
        private const int Bloque = 1024 * 1024;
        private readonly Repository repo;
        private readonly ObjectStore replica;

        public RestoreEngine(Repository repo, ObjectStore replica)
        {
            this.repo = repo;
            this.replica = replica;
        }

        /// <param name="rutas">null = todo el respaldo.</param>
        public ResultadoRestauracion Restaurar(RepoLock candado, SnapshotManifest m, string destino,
            ICollection<string> rutas, IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            if (candado == null) throw new ArgumentNullException("candado");
            if (string.IsNullOrWhiteSpace(destino)) throw new RebithException("Elija la carpeta destino.");
            destino = Path.GetFullPath(destino);
            if (Rutas.SeTraslapan(destino, repo.Raiz))
                throw new RebithException("No se puede restaurar dentro del deposito.");

            List<FileEntry> archivos = rutas == null
                ? m.Files.ToList()
                : m.Files.Where(f => rutas.Contains(f.Path)).ToList();
            var r = new ResultadoRestauracion { Destino = destino };
            Directory.CreateDirectory(destino);

            for (int i = 0; i < archivos.Count; i++)
            {
                cancelar.ThrowIfCancellationRequested();
                FileEntry f = archivos[i];
                if (progreso != null) progreso.Report(new ProgressInfo { Current = i + 1, Total = archivos.Count, Item = f.Path, Stage = "Restaurando" });
                string final;
                try
                {
                    final = Rutas.UnirSegura(destino, m.IsFolder ? f.Path : Path.GetFileName(f.Path));
                }
                catch (RebithException ex)
                {
                    r.Fallos.Add(new FalloRestauracion { Ruta = f.Path, Motivo = ex.Message });
                    continue;
                }

                try
                {
                    if (YaEsIgual(final, f))
                    {
                        r.SinCambios++;
                        continue;
                    }
                    string motivo = RestaurarUno(f, final, cancelar);
                    if (motivo == null) r.Restaurados++;
                    else r.Fallos.Add(new FalloRestauracion { Ruta = f.Path, Motivo = motivo });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    r.Fallos.Add(new FalloRestauracion { Ruta = f.Path, Motivo = ex.Message });
                }
            }
            Log.Info(string.Format("Restauracion de {0} en {1}: {2} restaurados, {3} sin cambios, {4} fallos",
                m.Id, destino, r.Restaurados, r.SinCambios, r.Fallos.Count));
            return r;
        }

        /// <summary>Si el destino ya tiene exactamente ese contenido, no hace falta escribirlo.</summary>
        private static bool YaEsIgual(string ruta, FileEntry f)
        {
            var info = new FileInfo(ruta);
            if (!info.Exists || info.Length != f.Size) return false;
            try
            {
                return Verifier.HashDeArchivo(ruta, CancellationToken.None) == f.Hash;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Devuelve null si salio bien, o el motivo del fallo.</summary>
        private string RestaurarUno(FileEntry f, string final, CancellationToken cancelar)
        {
            string carpeta = Path.GetDirectoryName(final);
            Directory.CreateDirectory(carpeta);
            string temporal = Path.Combine(carpeta, "." + Path.GetFileName(final) + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".rebith-tmp");
            try
            {
                string problema = Descomprimir(repo.Objetos, f, temporal, cancelar);
                if (problema != null && replica != null)
                {
                    string problemaReplica = Descomprimir(replica, f, temporal, cancelar);
                    if (problemaReplica == null)
                    {
                        Log.Advertencia("Se restauro " + f.Path + " desde la replica porque el deposito principal fallo: " + problema);
                        problema = null;
                    }
                }
                if (problema != null) return "El respaldo de este archivo esta danado: " + problema;

                File.SetLastWriteTimeUtc(temporal, new DateTime(f.MtimeUtcTicks, DateTimeKind.Utc));
                for (int intento = 1; ; intento++)
                {
                    try
                    {
                        if (File.Exists(final))
                        {
                            AtomicFile.QuitarSoloLectura(final);
                            File.Replace(temporal, final, null, true);
                        }
                        else
                        {
                            File.Move(temporal, final);
                        }
                        break;
                    }
                    catch (IOException ex)
                    {
                        if (intento >= 3) return "En uso o bloqueado por otro programa: " + ex.Message;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        if (intento >= 3) return "Sin permiso para escribir: " + ex.Message;
                    }
                    if (cancelar.WaitHandle.WaitOne(700 * intento)) cancelar.ThrowIfCancellationRequested();
                }
                try
                {
                    File.SetLastWriteTimeUtc(final, new DateTime(f.MtimeUtcTicks, DateTimeKind.Utc));
                }
                catch
                {
                    // La fecha es un detalle: el contenido ya esta correcto.
                }
                return null;
            }
            finally
            {
                AtomicFile.BorrarSinError(temporal);
            }
        }

        /// <summary>Descomprime al temporal calculando la huella. null = correcto.</summary>
        private static string Descomprimir(ObjectStore almacen, FileEntry f, string temporal, CancellationToken cancelar)
        {
            if (!almacen.Existe(f.Hash)) return "no existe el contenido en " + almacen.Raiz;
            try
            {
                using (Stream entrada = almacen.AbrirContenido(f.Hash))
                using (var salida = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None, Bloque))
                using (var sha = SHA256.Create())
                {
                    byte[] buffer = new byte[Bloque];
                    long total = 0;
                    int leidos;
                    while ((leidos = entrada.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancelar.ThrowIfCancellationRequested();
                        sha.TransformBlock(buffer, 0, leidos, null, 0);
                        salida.Write(buffer, 0, leidos);
                        total += leidos;
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    salida.Flush(true);
                    if (Hashes.Hex(sha.Hash) != f.Hash) return "la huella no coincide";
                    if (total != f.Size) return "el tamano no coincide";
                }
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException ex) when (!(ex is FileNotFoundException))
            {
                // Puede ser el disco destino (lleno): eso no es respaldo danado.
                if (EsErrorDelDestino(ex)) throw;
                return ex.Message;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static bool EsErrorDelDestino(IOException ex)
        {
            const int DiscoLleno = unchecked((int)0x80070070);
            const int DiscoLleno2 = unchecked((int)0x80070027);
            return ex.HResult == DiscoLleno || ex.HResult == DiscoLleno2;
        }
    }
}
