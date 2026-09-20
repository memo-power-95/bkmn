using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Rebith.Core
{
    /// <summary>
    /// Bitacora tecnica del programa (no es la auditoria de usuarios).
    /// Un archivo por dia. Nunca lanza excepciones: si no puede escribir, sigue.
    /// </summary>
    public static class Log
    {
        private static readonly object candado = new object();
        private static string carpeta;
        private const int DiasQueSeGuardan = 90;

        public static string Carpeta { get { return carpeta; } }

        public static void Configurar(string carpetaLogs)
        {
            try
            {
                Directory.CreateDirectory(carpetaLogs);
                lock (candado) carpeta = carpetaLogs;
                BorrarViejos();
            }
            catch
            {
            }
        }

        public static void Info(string texto) { Escribir("INFO", texto); }
        public static void Advertencia(string texto) { Escribir("AVISO", texto); }
        public static void Error(string texto) { Escribir("ERROR", texto); }

        public static void Error(string texto, Exception ex)
        {
            Escribir("ERROR", texto + (ex == null ? "" : " | " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace));
        }

        private static void Escribir(string tipo, string texto)
        {
            try
            {
                string destino = carpeta;
                if (destino == null) return;
                string linea = string.Format(CultureInfo.InvariantCulture, "{0} [{1}] [{2}] {3}{4}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    tipo, Thread.CurrentThread.ManagedThreadId, texto, Environment.NewLine);
                string archivo = Path.Combine(destino, "rebith_" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
                lock (candado)
                {
                    for (int intento = 0; intento < 3; intento++)
                    {
                        try
                        {
                            using (var fs = new FileStream(archivo, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                            {
                                byte[] bytes = Encoding.UTF8.GetBytes(linea);
                                fs.Write(bytes, 0, bytes.Length);
                            }
                            return;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(50);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void BorrarViejos()
        {
            try
            {
                foreach (string archivo in Directory.GetFiles(carpeta, "rebith_*.log"))
                {
                    if ((DateTime.Now - File.GetLastWriteTime(archivo)).TotalDays > DiasQueSeGuardan)
                        File.Delete(archivo);
                }
            }
            catch
            {
            }
        }
    }
}
