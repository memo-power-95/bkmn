using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    internal static class Program
    {
        /// <summary>
        /// Rebith.exe              abre la ventana.
        /// Rebith.exe /programado  corre sin ventana lo que toque (respaldos programados y
        ///                         revision automatica) y termina. Es lo que llama el
        ///                         Programador de tareas de Windows.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Log.Configurar(Path.Combine(RebithService.CarpetaLocal, "logs"));
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Log.Error("Error no controlado (la aplicacion se cierra)", e.ExceptionObject as Exception);

            if (args.Any(a => string.Equals(a, "/programado", StringComparison.OrdinalIgnoreCase)))
                return ModoProgramado();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ErrorInesperado(e.Exception);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Tema.Iniciar();

            bool primera;
            using (var unica = new Mutex(true, "Local\\Rebith.Ventana", out primera))
            {
                if (!primera)
                {
                    MessageBox.Show("Rebith ya esta abierto en esta sesion de Windows.", "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                RebithService servicio = AbrirDeposito();
                if (servicio == null) return 1;
                Application.Run(new VentanaPrincipal(servicio));
                GC.KeepAlive(unica);
            }
            return 0;
        }

        private static int ModoProgramado()
        {
            try
            {
                MachineSettings ajustes = RebithService.LeerAjustesLocales();
                string ruta = string.IsNullOrWhiteSpace(ajustes.RepoPath) ? RebithService.DepositoPredeterminado() : ajustes.RepoPath;
                RebithService servicio = RebithService.Abrir(ruta);
                ResultadoProgramado r = servicio.EjecutarPendientes(false, CancellationToken.None);
                return r.HuboErrores ? 2 : 0;
            }
            catch (Exception ex)
            {
                Log.Error("Modo programado", ex);
                return 1;
            }
        }

        /// <summary>
        /// Abre el deposito guardado para esta PC. Si no se puede (disco de red caido, carpeta
        /// borrada), deja elegir otra carpeta o reintentar en lugar de cerrarse.
        /// </summary>
        private static RebithService AbrirDeposito()
        {
            MachineSettings ajustes = RebithService.LeerAjustesLocales();
            string ruta = string.IsNullOrWhiteSpace(ajustes.RepoPath) ? RebithService.DepositoPredeterminado() : ajustes.RepoPath;
            while (true)
            {
                try
                {
                    RebithService s = RebithService.Abrir(ruta);
                    if (ajustes.RepoPath != s.Repo.Raiz)
                    {
                        ajustes.RepoPath = s.Repo.Raiz;
                        try { RebithService.GuardarAjustesLocales(ajustes); } catch (Exception ex) { Log.Error("Guardar equipo.json", ex); }
                    }
                    return s;
                }
                catch (Exception ex)
                {
                    Log.Error("No se pudo abrir el deposito " + ruta, ex);
                    DialogResult r = MessageBox.Show(
                        "No se pudo abrir el deposito de respaldos:\n\n" + ruta + "\n\n" + ex.Message +
                        "\n\nSi = elegir otra carpeta\nNo = reintentar\nCancelar = salir",
                        "Rebith", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (r == DialogResult.Cancel) return null;
                    if (r == DialogResult.Yes)
                    {
                        using (var d = new FolderBrowserDialog { Description = "Carpeta del deposito (vacia para uno nuevo, o un deposito existente)" })
                        {
                            if (d.ShowDialog() == DialogResult.OK) ruta = d.SelectedPath;
                        }
                    }
                }
            }
        }

        /// <summary>Un error inesperado en la ventana se anota y se avisa, pero NO cierra el programa.</summary>
        public static void ErrorInesperado(Exception ex)
        {
            Log.Error("Error inesperado en la ventana", ex);
            try
            {
                MessageBox.Show("Ocurrio un error inesperado, pero el programa sigue funcionando y los respaldos no se afectaron.\n\n" +
                    ex.Message + "\n\nEl detalle quedo en el log: " + Log.Carpeta, "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
