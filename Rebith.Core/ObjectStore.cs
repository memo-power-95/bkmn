using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

namespace Rebith.Core
{
    /// <summary>El problema fue al LEER el archivo original (en uso, sin permiso, cambio). No es culpa del deposito.</summary>
    public class SourceFileException : Exception
    {
        public SourceFileException(string message, Exception inner) : base(message, inner) { }
    }

    public enum EstadoObjeto
    {
        Ok,
        Falta,
        Danado
    }

    public class ResultadoObjeto
    {
        public EstadoObjeto Estado;
        public string Detalle = "";
        public long TamanoOriginal;
    }

    public class ObjetoGuardado
    {
        public string Hash;
        public long Tamano;
        public bool EsNuevo;
        public long BytesGuardados;
        /// <summary>Habia una copia danada de este contenido y se reemplazo.</summary>
        public bool Reparado;
    }

    /// <summary>
    /// Contenido deduplicado: cada contenido distinto se guarda una sola vez como
    /// objects/ab/abcdef....bin comprimido en formato zlib (el mismo de la version en
    /// Python, asi los respaldos viejos siguen sirviendo).
    /// Todo se procesa por partes de 1 MB: un archivo de 400 MB usa la misma memoria
    /// que uno de 1 KB.
    /// </summary>
    public class ObjectStore
    {
        private const int Bloque = 1024 * 1024;
        private readonly string raiz;
        private readonly string carpetaTemporal;

        public ObjectStore(string carpetaObjetos, string carpetaTemporal)
        {
            raiz = carpetaObjetos;
            this.carpetaTemporal = carpetaTemporal;
            Directory.CreateDirectory(raiz);
            Directory.CreateDirectory(carpetaTemporal);
        }

        public string Raiz { get { return raiz; } }

        public string RutaDe(string hash)
        {
            if (!Hashes.EsHashValido(hash)) throw new RebithException("Hash invalido: " + hash);
            return Path.Combine(raiz, hash.Substring(0, 2), hash + ".bin");
        }

        public bool Existe(string hash)
        {
            try
            {
                var info = new FileInfo(RutaDe(hash));
                return info.Exists && info.Length > 2;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lee el archivo original UNA vez: calcula SHA-256 y comprime al mismo tiempo en un
        /// temporal. Si el contenido ya existia, borra el temporal (deduplicado).
        /// Si el contenido existe pero esta en "danados" (lo marco la verificacion), se
        /// reescribe con la copia sana que se acaba de leer: el respaldo se cura solo.
        /// Errores al leer el original -> SourceFileException. Errores al escribir en el
        /// deposito (disco lleno, red caida) -> se propagan tal cual.
        /// </summary>
        public ObjetoGuardado GuardarDesdeArchivo(string archivo, System.Collections.Generic.ICollection<string> danados, CancellationToken cancelar)
        {
            string temporal = Path.Combine(carpetaTemporal, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                string hash;
                long tamano = 0;
                FileStream origen;
                try
                {
                    origen = new FileStream(archivo, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete, Bloque, FileOptions.SequentialScan);
                }
                catch (Exception ex)
                {
                    throw new SourceFileException(ex.Message, ex);
                }

                using (origen)
                using (var sha = SHA256.Create())
                using (var salida = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, Bloque))
                {
                    var adler = new Adler32();
                    salida.WriteByte(0x78);
                    salida.WriteByte(0x9C);
                    byte[] buffer = new byte[Bloque];
                    using (var deflate = new DeflateStream(salida, CompressionLevel.Optimal, true))
                    {
                        while (true)
                        {
                            cancelar.ThrowIfCancellationRequested();
                            int leidos;
                            try
                            {
                                leidos = origen.Read(buffer, 0, buffer.Length);
                            }
                            catch (Exception ex)
                            {
                                throw new SourceFileException(ex.Message, ex);
                            }
                            if (leidos <= 0) break;
                            sha.TransformBlock(buffer, 0, leidos, null, 0);
                            adler.Actualizar(buffer, 0, leidos);
                            deflate.Write(buffer, 0, leidos);
                            tamano += leidos;
                        }
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    hash = Hashes.Hex(sha.Hash);
                    uint a = adler.Valor;
                    salida.WriteByte((byte)(a >> 24));
                    salida.WriteByte((byte)(a >> 16));
                    salida.WriteByte((byte)(a >> 8));
                    salida.WriteByte((byte)a);
                    salida.Flush(true);
                }

                var resultado = new ObjetoGuardado { Hash = hash, Tamano = tamano };
                string destino = RutaDe(hash);
                bool estabaDanado = danados != null && danados.Contains(hash);
                if (Existe(hash) && !estabaDanado)
                {
                    resultado.EsNuevo = false;
                    return resultado;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destino));
                resultado.BytesGuardados = new FileInfo(temporal).Length;
                ColocarObjeto(temporal, destino);
                resultado.EsNuevo = true;
                resultado.Reparado = estabaDanado;
                return resultado;
            }
            finally
            {
                AtomicFile.BorrarSinError(temporal);
            }
        }

        /// <summary>Pone un .bin en su lugar. Si ya habia uno danado o vacio, lo reemplaza.</summary>
        private static void ColocarObjeto(string temporal, string destino)
        {
            if (File.Exists(destino))
                AtomicFile.Publicar(temporal, destino, false);
            else
            {
                try
                {
                    File.Move(temporal, destino);
                }
                catch (IOException) when (File.Exists(destino))
                {
                    // Otro proceso lo acaba de escribir con el mismo contenido: esta bien.
                }
            }
        }

        /// <summary>Abre el contenido descomprimido. Quien llama debe cerrar el stream.</summary>
        public Stream AbrirContenido(string hash)
        {
            string ruta = RutaDe(hash);
            var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, Bloque, FileOptions.SequentialScan);
            try
            {
                int cmf = fs.ReadByte();
                int flg = fs.ReadByte();
                if (cmf < 0 || flg < 0 || (cmf & 0x0F) != 8 || ((cmf << 8) + flg) % 31 != 0 || (flg & 0x20) != 0)
                    throw new InvalidDataException("Encabezado zlib invalido.");
                return new DeflateStream(fs, CompressionMode.Decompress, false);
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }

        /// <summary>Descomprime todo el objeto y confirma que su SHA-256 sea el esperado.</summary>
        public ResultadoObjeto Verificar(string hash, long tamanoEsperado, CancellationToken cancelar)
        {
            var r = new ResultadoObjeto();
            if (!File.Exists(RutaDe(hash)))
            {
                r.Estado = EstadoObjeto.Falta;
                r.Detalle = "No existe el archivo del respaldo.";
                return r;
            }
            try
            {
                using (Stream contenido = AbrirContenido(hash))
                using (var sha = SHA256.Create())
                {
                    byte[] buffer = new byte[Bloque];
                    long total = 0;
                    int leidos;
                    while ((leidos = contenido.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancelar.ThrowIfCancellationRequested();
                        sha.TransformBlock(buffer, 0, leidos, null, 0);
                        total += leidos;
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                    r.TamanoOriginal = total;
                    string real = Hashes.Hex(sha.Hash);
                    if (real != hash)
                    {
                        r.Estado = EstadoObjeto.Danado;
                        r.Detalle = "El contenido no coincide con su huella (SHA-256).";
                    }
                    else if (tamanoEsperado >= 0 && total != tamanoEsperado)
                    {
                        r.Estado = EstadoObjeto.Danado;
                        r.Detalle = "Tamano distinto: " + total + " en vez de " + tamanoEsperado + ".";
                    }
                    else
                    {
                        r.Estado = EstadoObjeto.Ok;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                r.Estado = EstadoObjeto.Danado;
                r.Detalle = "No se puede descomprimir: " + ex.Message;
            }
            return r;
        }

        /// <summary>
        /// Copia un objeto a otro deposito (replica) y confirma la copia.
        /// Devuelve true si el destino quedo sano.
        /// </summary>
        public bool CopiarA(ObjectStore destino, string hash, CancellationToken cancelar)
        {
            string origen = RutaDe(hash);
            string final = destino.RutaDe(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(final));
            string temporal = Path.Combine(destino.carpetaTemporal, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.Copy(origen, temporal, true);
                using (var fs = new FileStream(temporal, FileMode.Open, FileAccess.ReadWrite))
                    fs.Flush(true);
                ColocarObjeto(temporal, final);
            }
            finally
            {
                AtomicFile.BorrarSinError(temporal);
            }
            return destino.Verificar(hash, -1, cancelar).Estado == EstadoObjeto.Ok;
        }

        public void Borrar(string hash)
        {
            AtomicFile.BorrarSinError(RutaDe(hash));
        }

        private class Adler32
        {
            private uint a = 1, b = 0;
            private const uint Mod = 65521;

            public void Actualizar(byte[] datos, int inicio, int cuenta)
            {
                int fin = inicio + cuenta;
                int i = inicio;
                while (i < fin)
                {
                    int tramo = Math.Min(3800, fin - i);
                    for (int k = 0; k < tramo; k++)
                    {
                        a += datos[i++];
                        b += a;
                    }
                    a %= Mod;
                    b %= Mod;
                }
            }

            public uint Valor { get { return (b << 16) | a; } }
        }
    }
}
