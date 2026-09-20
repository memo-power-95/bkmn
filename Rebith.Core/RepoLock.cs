using System;
using System.Diagnostics;
using System.IO;

namespace Rebith.Core
{
    /// <summary>
    /// Solo UNA operacion que cambia el deposito a la vez (respaldo, restaurar, borrar,
    /// verificar a fondo), aunque venga de otra ventana o del respaldo programado.
    /// Se usa un archivo abierto en exclusiva: si el programa se cierra o se cae,
    /// Windows suelta el archivo y el candado se libera solo (no se queda trabado).
    /// </summary>
    public sealed class RepoLock : IDisposable
    {
        private FileStream archivo;
        private readonly string rutaInfo;

        public string Operacion { get; private set; }

        private RepoLock(FileStream archivo, string rutaInfo, string operacion)
        {
            this.archivo = archivo;
            this.rutaInfo = rutaInfo;
            Operacion = operacion;
        }

        public static RepoLock Tomar(string carpetaDeposito, string operacion, string usuario)
        {
            string ruta = Path.Combine(carpetaDeposito, "deposito.lock");
            string rutaInfo = Path.Combine(carpetaDeposito, "deposito.lock.info");
            FileStream fs;
            try
            {
                fs = new FileStream(ruta, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 16, FileOptions.None);
            }
            catch (IOException)
            {
                throw new RepoBusyException("El deposito esta ocupado: " + LeerQuienLoTiene(rutaInfo) +
                    ". Espere a que termine o cancele esa operacion.");
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new RebithException("No hay permiso para escribir en el deposito: " + ex.Message, ex);
            }

            try
            {
                string info = string.Format("{0} | usuario: {1} | equipo: {2} | proceso: {3} | desde: {4}",
                    operacion, string.IsNullOrEmpty(usuario) ? "-" : usuario, Environment.MachineName,
                    Process.GetCurrentProcess().Id, Tiempo.Legible(Tiempo.AhoraIso()));
                File.WriteAllText(rutaInfo, info);
            }
            catch
            {
                // Solo es informativo.
            }
            return new RepoLock(fs, rutaInfo, operacion);
        }

        private static string LeerQuienLoTiene(string rutaInfo)
        {
            try
            {
                if (File.Exists(rutaInfo)) return File.ReadAllText(rutaInfo).Trim();
            }
            catch
            {
            }
            return "otra operacion en curso";
        }

        public void Dispose()
        {
            FileStream fs = archivo;
            archivo = null;
            if (fs == null) return;
            try
            {
                File.WriteAllText(rutaInfo, "libre");
            }
            catch
            {
            }
            try
            {
                fs.Dispose();
            }
            catch
            {
            }
        }
    }
}
