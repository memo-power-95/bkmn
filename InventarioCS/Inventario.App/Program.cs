using System;
using System.IO;
using System.Windows.Forms;
using Inventario.Core;

namespace Inventario.App
{
    internal static class Program
    {
        /// <summary>
        /// Inventario.exe                 usa retrabajo_pcb.db junto al .exe (igual que inv.py:
        ///                                si se pone en la misma carpeta que inv_v3.exe, comparten datos).
        /// Inventario.exe "ruta\base.db"  usa esa base.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ErrorInesperado(e.Exception);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Tema.Iniciar();

            string ruta = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? Path.GetFullPath(args[0])
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "retrabajo_pcb.db");

            BaseInventario db;
            try
            {
                db = new BaseInventario(ruta);
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show("No se encontró winsqlite3.dll. Este programa necesita Windows 10 u 11.",
                    "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir la base de datos:\n\n" + ruta + "\n\n" + ex.Message,
                    "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            using (db) Application.Run(new VentanaInventario(db));
            return 0;
        }

        /// <summary>Un error inesperado se avisa, pero NO cierra el programa.</summary>
        public static void ErrorInesperado(Exception ex)
        {
            try
            {
                MessageBox.Show("Ocurrió un error inesperado, pero el programa sigue funcionando.\n\n" + ex.Message,
                    "Inventario", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
}
