using System;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    /// <summary>
    /// Quien esta usando la ventana. Sin sesion se puede VER todo (estado, historial,
    /// verificar contra el origen); para cambiar algo se pide usuario y contrasena.
    /// La sesion se cierra sola tras unos minutos sin tocar el teclado ni el mouse.
    /// </summary>
    public class Sesion : IMessageFilter
    {
        private readonly RebithService servicio;
        private DateTime ultimaActividad = DateTime.Now;

        public UserInfo Usuario { get; private set; }
        public event EventHandler Cambio;

        public Sesion(RebithService servicio)
        {
            this.servicio = servicio;
            Application.AddMessageFilter(this);
        }

        public string Nombre { get { return Usuario == null ? "" : Usuario.Username; } }
        public bool Activa { get { return Usuario != null; } }

        /// <summary>Nombre para la auditoria: el usuario, o "sin sesion".</summary>
        public string ParaAuditoria { get { return Usuario == null ? "sin sesion" : Usuario.Username; } }

        public bool Tiene(Permiso permiso)
        {
            return UserStore.Tiene(Usuario, permiso);
        }

        /// <summary>
        /// Confirma que el usuario puede hacer la accion. Si no hay sesion, abre el login.
        /// Relee al usuario por si otro administrador le quito el permiso.
        /// </summary>
        public bool Exigir(IWin32Window dueno, Permiso permiso, string accion)
        {
            if (Usuario != null)
            {
                UserInfo fresco = servicio.Usuarios.Refrescar(Usuario.Username);
                if (fresco == null || !fresco.IsActive)
                {
                    Salir("usuario desactivado");
                    MessageBox.Show(dueno, "Su usuario fue desactivado. Entre con otro usuario.", "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                Usuario = fresco;
            }
            if (Usuario == null && !Entrar(dueno, "Para " + accion + " entre con su usuario.")) return false;
            if (Tiene(permiso))
            {
                Tocar();
                return true;
            }
            MessageBox.Show(dueno, "El usuario " + Usuario.Username + " no tiene permiso para " + accion + ".",
                "Sin permiso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            servicio.Auditoria.Registrar(Usuario.Username, "sin_permiso", accion);
            return false;
        }

        public bool Entrar(IWin32Window dueno, string motivo)
        {
            using (var d = new DialogoLogin(servicio, motivo))
            {
                if (d.ShowDialog(dueno) != DialogResult.OK || d.Resultado == null) return false;
                Usuario = d.Resultado;
            }
            Tocar();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
            return true;
        }

        public void Salir(string motivo)
        {
            if (Usuario == null) return;
            servicio.Auditoria.Registrar(Usuario.Username, "logout", motivo);
            Usuario = null;
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }

        public void Tocar()
        {
            ultimaActividad = DateTime.Now;
        }

        /// <summary>Se llama cada pocos segundos desde un Timer de la ventana.</summary>
        public void RevisarInactividad(bool hayOperacionEnCurso)
        {
            if (Usuario == null || hayOperacionEnCurso) return;
            int minutos = 15;
            try
            {
                minutos = servicio.Repo.LeerAjustes().SessionTimeoutMinutes;
            }
            catch
            {
            }
            if ((DateTime.Now - ultimaActividad).TotalMinutes >= minutos)
                Salir("inactividad de " + minutos + " minutos");
        }

        public bool PreFilterMessage(ref Message m)
        {
            // Teclado o mouse = actividad.
            const int WM_KEYDOWN = 0x0100, WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_KEYDOWN || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_RBUTTONDOWN || m.Msg == WM_MOUSEWHEEL)
                ultimaActividad = DateTime.Now;
            return false;
        }
    }
}
