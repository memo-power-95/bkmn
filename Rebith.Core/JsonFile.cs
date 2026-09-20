using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Rebith.Core
{
    /// <summary>
    /// Lectura y escritura de JSON sin dejar archivos a medias.
    /// Escribir: se escribe un temporal, se fuerza a disco y se reemplaza el archivo real.
    /// Si se va la luz a la mitad, queda el archivo viejo completo (nunca uno cortado).
    /// Con copiaDeSeguridad, la version anterior queda como .bak y Leer la usa si el
    /// archivo principal esta danado.
    /// </summary>
    public static class JsonFile
    {
        public static string Serializar<T>(T valor)
        {
            var serializador = new DataContractJsonSerializer(typeof(T));
            using (var memoria = new MemoryStream())
            {
                using (var escritor = JsonReaderWriterFactory.CreateJsonWriter(memoria, new UTF8Encoding(false), false, true, "  "))
                {
                    serializador.WriteObject(escritor, valor);
                    escritor.Flush();
                }
                return new UTF8Encoding(false).GetString(memoria.ToArray());
            }
        }

        public static T Deserializar<T>(string texto)
        {
            var serializador = new DataContractJsonSerializer(typeof(T));
            byte[] bytes = new UTF8Encoding(false).GetBytes(QuitarBom(texto));
            using (var memoria = new MemoryStream(bytes))
            {
                object valor = serializador.ReadObject(memoria);
                if (valor == null) throw new InvalidDataException("El archivo JSON esta vacio.");
                return (T)valor;
            }
        }

        public static void Escribir<T>(string ruta, T valor, bool copiaDeSeguridad)
        {
            AtomicFile.WriteAllText(ruta, Serializar(valor), copiaDeSeguridad);
        }

        /// <summary>
        /// Lee el archivo. Si no existe devuelve null. Si esta danado intenta el .bak.
        /// Si ambos fallan lanza RebithException (nunca devuelve datos inventados).
        /// </summary>
        public static T Leer<T>(string ruta) where T : class
        {
            Exception primerError = null;
            if (File.Exists(ruta))
            {
                try
                {
                    return Deserializar<T>(LeerTextoCompartido(ruta));
                }
                catch (Exception ex)
                {
                    primerError = ex;
                }
            }

            string respaldo = ruta + ".bak";
            if (File.Exists(respaldo))
            {
                try
                {
                    T valor = Deserializar<T>(LeerTextoCompartido(respaldo));
                    Log.Advertencia("Se uso la copia .bak de " + ruta +
                        (primerError != null ? " porque el archivo principal esta danado: " + primerError.Message : " porque el archivo principal no existe."));
                    return valor;
                }
                catch (Exception ex)
                {
                    if (primerError == null) primerError = ex;
                }
            }

            if (primerError != null)
                throw new RebithException("No se pudo leer " + ruta + ": " + primerError.Message, primerError);
            return null;
        }

        /// <summary>Lee texto aunque otro proceso tenga el archivo abierto.</summary>
        public static string LeerTextoCompartido(string ruta)
        {
            for (int intento = 1; ; intento++)
            {
                try
                {
                    using (var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var lector = new StreamReader(fs, Encoding.UTF8, true))
                    {
                        return lector.ReadToEnd();
                    }
                }
                catch (IOException) when (intento < 5 && File.Exists(ruta))
                {
                    Thread.Sleep(100 * intento);
                }
            }
        }

        private static string QuitarBom(string texto)
        {
            if (!string.IsNullOrEmpty(texto) && texto[0] == '﻿') return texto.Substring(1);
            return texto ?? "";
        }
    }

    public static class AtomicFile
    {
        public static void WriteAllText(string ruta, string contenido, bool copiaDeSeguridad)
        {
            WriteAllBytes(ruta, new UTF8Encoding(false).GetBytes(contenido), copiaDeSeguridad);
        }

        public static void WriteAllBytes(string ruta, byte[] contenido, bool copiaDeSeguridad)
        {
            string carpeta = Path.GetDirectoryName(Path.GetFullPath(ruta));
            Directory.CreateDirectory(carpeta);
            string temporal = Path.Combine(carpeta, "." + Path.GetFileName(ruta) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var fs = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    fs.Write(contenido, 0, contenido.Length);
                    fs.Flush(true);
                }
                Publicar(temporal, ruta, copiaDeSeguridad);
            }
            finally
            {
                BorrarSinError(temporal);
            }
        }

        /// <summary>Pone el temporal en lugar del destino (mismo disco), con reintentos si esta en uso.</summary>
        public static void Publicar(string temporal, string destino, bool copiaDeSeguridad)
        {
            for (int intento = 1; ; intento++)
            {
                try
                {
                    if (File.Exists(destino))
                    {
                        QuitarSoloLectura(destino);
                        string bak = copiaDeSeguridad ? destino + ".bak" : null;
                        if (bak != null) QuitarSoloLectura(bak);
                        File.Replace(temporal, destino, bak, true);
                    }
                    else
                    {
                        File.Move(temporal, destino);
                    }
                    return;
                }
                catch (IOException) when (intento < 5)
                {
                    Thread.Sleep(200 * intento);
                }
                catch (UnauthorizedAccessException) when (intento < 5)
                {
                    Thread.Sleep(200 * intento);
                }
            }
        }

        public static void QuitarSoloLectura(string ruta)
        {
            try
            {
                if (!File.Exists(ruta)) return;
                FileAttributes atributos = File.GetAttributes(ruta);
                if ((atributos & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(ruta, atributos & ~FileAttributes.ReadOnly);
            }
            catch
            {
            }
        }

        public static void BorrarSinError(string ruta)
        {
            try
            {
                if (ruta != null && File.Exists(ruta))
                {
                    QuitarSoloLectura(ruta);
                    File.Delete(ruta);
                }
            }
            catch
            {
            }
        }
    }
}
