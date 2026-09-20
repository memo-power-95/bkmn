using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    public class PaginaAdministracion : PaginaBase
    {
        private readonly DataGridView gridUsuarios = Tema.Tabla();
        private readonly DataGridView gridAuditoria = Tema.Tabla();
        private readonly Banner cadena = new Banner();
        private readonly Label bloqueo = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = Tema.Subtitulo, ForeColor = Tema.TextoSuave };
        private readonly Panel contenidoProtegido = new Panel { Dock = DockStyle.Fill };

        public PaginaAdministracion(VentanaPrincipal v) : base(v)
        {
            gridUsuarios.Columns.AddRange(Tema.Columna("Usuario", "Usuario", 100), Tema.Columna("Rol", "Rol", 70), Tema.Columna("Estado", "Estado", 70),
                Tema.Columna("Respaldar", "Respaldar", 60), Tema.Columna("Restaurar", "Restaurar", 60), Tema.Columna("Borrar", "Borrar", 50),
                Tema.Columna("Usuarios", "Usuarios", 60), Tema.Columna("Seguridad", "Seguridad", 60), Tema.Columna("Creado", "Creado", 90));
            gridUsuarios.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Editar(); };
            gridAuditoria.Columns.AddRange(Tema.Columna("Fecha", "Fecha", 90), Tema.Columna("Usuario", "Usuario", 70), Tema.Columna("Equipo", "Equipo", 70),
                Tema.Columna("Accion", "Accion", 110), Tema.Columna("Detalle", "Detalle", 360));

            var botonesUsuarios = Tema.FilaBotones();
            botonesUsuarios.Controls.Add(Tema.Boton("+ Nuevo usuario", (s, e) => Nuevo(), true));
            botonesUsuarios.Controls.Add(Tema.Boton("Editar", (s, e) => Editar()));
            botonesUsuarios.Controls.Add(Tema.BotonPeligro("Eliminar", (s, e) => Eliminar()));
            var tabUsuarios = new TabPage("Usuarios y permisos") { BackColor = Tema.Fondo, Padding = new Padding(Tema.S(10)) };
            tabUsuarios.Controls.Add(gridUsuarios);
            tabUsuarios.Controls.Add(botonesUsuarios);

            var botonesAuditoria = Tema.FilaBotones();
            botonesAuditoria.Controls.Add(Tema.Boton("Comprobar que nadie la altero", (s, e) => ComprobarCadena(), true));
            botonesAuditoria.Controls.Add(Tema.Boton("Abrir archivo", (s, e) => PaginaVerificar.AbrirCarpeta(Servicio.Repo.CarpetaAuditoria)));
            var tabAuditoria = new TabPage("Auditoria") { BackColor = Tema.Fondo, Padding = new Padding(Tema.S(10)) };
            tabAuditoria.Controls.Add(gridAuditoria);
            tabAuditoria.Controls.Add(cadena);
            tabAuditoria.Controls.Add(botonesAuditoria);

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = Tema.Normal, Padding = new Point(Tema.S(14), Tema.S(6)) };
            tabs.TabPages.Add(tabUsuarios);
            tabs.TabPages.Add(tabAuditoria);
            contenidoProtegido.Controls.Add(tabs);

            Controls.Add(contenidoProtegido);
            Controls.Add(bloqueo);
            Controls.Add(Tema.EncabezadoPagina("Usuarios y auditoria"));
        }

        protected override void AlRefrescar()
        {
            bool puedeUsuarios = Sesion.Tiene(Permiso.GestionarUsuarios);
            bool puedeSeguridad = Sesion.Tiene(Permiso.GestionarSeguridad);
            if (!puedeUsuarios && !puedeSeguridad)
            {
                contenidoProtegido.Visible = false;
                bloqueo.Visible = true;
                bloqueo.Text = Sesion.Activa ? "Su usuario no tiene permiso para ver esta seccion." : "Entre con un usuario administrador para ver esta seccion.";
                return;
            }
            bloqueo.Visible = false;
            contenidoProtegido.Visible = true;

            gridUsuarios.Rows.Clear();
            foreach (UserInfo u in Servicio.Usuarios.Listar())
            {
                DateTime? bloqueado = Tiempo.Leer(u.LockedUntilLocal);
                string estado = !u.IsActive ? "Desactivado" : bloqueado.HasValue && bloqueado.Value > DateTime.Now ? "Bloqueado" : "Activo";
                int n = gridUsuarios.Rows.Add(u.Username, u.Role, estado, SiNo(u.EsAdmin || u.CanBackup), SiNo(u.EsAdmin || u.CanRestore),
                    SiNo(u.EsAdmin || u.CanDelete), SiNo(u.EsAdmin || u.CanManageUsers), SiNo(u.EsAdmin || u.CanManageSecurity), Tiempo.Legible(u.CreatedLocal));
                gridUsuarios.Rows[n].Tag = u;
                if (estado != "Activo") gridUsuarios.Rows[n].DefaultCellStyle.ForeColor = Tema.TextoSuave;
            }

            gridAuditoria.Rows.Clear();
            foreach (EventoAuditoria e in Servicio.Auditoria.Ultimos(500))
            {
                int n = gridAuditoria.Rows.Add(Tiempo.Legible(e.Fecha), e.Usuario, e.Equipo, e.Accion, e.Detalle);
                if (e.Accion.Contains("fallid") || e.Accion.Contains("bloque") || e.Accion.Contains("sin_permiso"))
                    gridAuditoria.Rows[n].DefaultCellStyle.ForeColor = Tema.Mal;
            }
        }

        private static string SiNo(bool v) { return v ? "Si" : "-"; }

        private UserInfo Seleccionado()
        {
            if (gridUsuarios.SelectedRows.Count == 0) return null;
            return gridUsuarios.SelectedRows[0].Tag as UserInfo;
        }

        private void Nuevo()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarUsuarios, "administrar usuarios")) return;
            using (var d = new DialogoUsuario(Servicio, Sesion.Usuario, null)) d.ShowDialog(this);
            AlRefrescar();
        }

        private void Editar()
        {
            UserInfo u = Seleccionado();
            if (u == null) return;
            if (!Sesion.Exigir(this, Permiso.GestionarUsuarios, "administrar usuarios")) return;
            using (var d = new DialogoUsuario(Servicio, Sesion.Usuario, u)) d.ShowDialog(this);
            AlRefrescar();
        }

        private void Eliminar()
        {
            UserInfo u = Seleccionado();
            if (u == null) return;
            if (!Sesion.Exigir(this, Permiso.GestionarUsuarios, "administrar usuarios")) return;
            if (!Confirmar("Eliminar al usuario " + u.Username + "? (Para bloquearlo temporalmente mejor desactivelo.)", "Eliminar usuario")) return;
            Seguro(() =>
            {
                Servicio.Usuarios.Eliminar(Sesion.Usuario, u.Username);
                AlRefrescar();
            });
        }

        private void ComprobarCadena()
        {
            ResultadoCadena r = Servicio.Auditoria.VerificarCadena();
            if (r.Integra) cadena.Mostrar(r.Detalle, Tema.BienFondo, Tema.Bien);
            else cadena.Mostrar("ALERTA: " + r.Detalle, Tema.MalFondo, Tema.Mal);
        }
    }

    public class DialogoUsuario : DialogoBase
    {
        private readonly RebithService servicio;
        private readonly UserInfo quien;
        private readonly UserInfo original;
        private readonly TextBox txtUsuario = new TextBox();
        private readonly TextBox txtContrasena = new TextBox { UseSystemPasswordChar = true };
        private readonly ComboBox cmbRol = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox chkActivo = new CheckBox { Text = "Activo", AutoSize = true, Checked = true };
        private readonly CheckBox chkRespaldar = new CheckBox { Text = "Respaldar y configurar objetivos", AutoSize = true, Checked = true };
        private readonly CheckBox chkRestaurar = new CheckBox { Text = "Restaurar", AutoSize = true, Checked = true };
        private readonly CheckBox chkBorrar = new CheckBox { Text = "Borrar respaldos y objetivos", AutoSize = true };
        private readonly CheckBox chkUsuarios = new CheckBox { Text = "Administrar usuarios", AutoSize = true };
        private readonly CheckBox chkSeguridad = new CheckBox { Text = "Configuracion y seguridad", AutoSize = true };

        public DialogoUsuario(RebithService servicio, UserInfo quien, UserInfo original) : base(original == null ? "Nuevo usuario" : "Editar usuario", 460)
        {
            this.servicio = servicio;
            this.quien = quien;
            this.original = original;
            cmbRol.Items.AddRange(Roles.Todos);
            AgregarCampo("Usuario", txtUsuario);
            AgregarCampo("Contrasena", txtContrasena);
            AgregarTexto(original == null ? "El usuario debera cambiarla la primera vez que entre." : "Deje vacio para no cambiarla. Si la escribe, el usuario debera cambiarla al entrar.", Tema.TextoSuave);
            AgregarCampo("Rol", cmbRol);
            var permisos = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            permisos.Controls.AddRange(new Control[] { chkActivo, chkRespaldar, chkRestaurar, chkBorrar, chkUsuarios, chkSeguridad });
            AgregarCampo("Permisos", permisos);
            AgregarTexto("Un admin tiene todos los permisos.", Tema.TextoSuave);
            Button guardar = AgregarBoton("Guardar", DialogResult.None, true);
            AgregarBoton("Cancelar", DialogResult.Cancel, false);
            guardar.Click += Guardar;
            cmbRol.SelectedIndexChanged += (s, e) => ActualizarPermisos();

            UserInfo u = original ?? new UserInfo { Role = Roles.Operador, IsActive = true, CanBackup = true, CanRestore = true };
            txtUsuario.Text = u.Username;
            cmbRol.SelectedItem = u.Role;
            chkActivo.Checked = u.IsActive;
            chkRespaldar.Checked = u.CanBackup;
            chkRestaurar.Checked = u.CanRestore;
            chkBorrar.Checked = u.CanDelete;
            chkUsuarios.Checked = u.CanManageUsers;
            chkSeguridad.Checked = u.CanManageSecurity;
            ActualizarPermisos();
        }

        private void ActualizarPermisos()
        {
            bool admin = (cmbRol.SelectedItem as string) == Roles.Admin;
            foreach (CheckBox c in new[] { chkRespaldar, chkRestaurar, chkBorrar, chkUsuarios, chkSeguridad })
            {
                c.Enabled = !admin;
                if (admin) c.Checked = true;
            }
        }

        private void Guardar(object sender, EventArgs e)
        {
            var datos = new UserInfo
            {
                Username = txtUsuario.Text,
                Role = cmbRol.SelectedItem as string ?? Roles.Operador,
                IsActive = chkActivo.Checked,
                CanBackup = chkRespaldar.Checked,
                CanRestore = chkRestaurar.Checked,
                CanDelete = chkBorrar.Checked,
                CanManageUsers = chkUsuarios.Checked,
                CanManageSecurity = chkSeguridad.Checked
            };
            try
            {
                servicio.Usuarios.Guardar(quien, original == null ? null : original.Username, datos, txtContrasena.Text);
            }
            catch (RebithException ex)
            {
                Error(ex.Message);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
