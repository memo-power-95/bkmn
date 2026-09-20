using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    public interface IPagina
    {
        /// <summary>Vuelve a leer los datos. Nunca debe lanzar excepciones hacia afuera.</summary>
        void Refrescar();
    }

    public enum Paginas
    {
        Inicio,
        Objetivos,
        Historial,
        Verificar,
        Configuracion,
        Administracion
    }

    public class VentanaPrincipal : Form
    {
        public RebithService Servicio { get; private set; }
        public Sesion Sesion { get; private set; }

        private readonly Panel contenido = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fondo, Padding = new Padding(Tema.S(22)) };
        private readonly Dictionary<Paginas, Control> paginas = new Dictionary<Paginas, Control>();
        private readonly Dictionary<Paginas, Button> botonesMenu = new Dictionary<Paginas, Button>();
        private Paginas actual = Paginas.Inicio;

        private readonly Label lblUsuario = new Label { AutoSize = true, Font = Tema.Negrita, ForeColor = Tema.Texto, Margin = new Padding(0, Tema.S(9), Tema.S(10), 0) };
        private readonly Button btnSesion;
        private readonly Button btnContrasena;
        private readonly Label lblDeposito = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave, Margin = new Padding(0, Tema.S(11), Tema.S(18), 0) };

        private readonly Panel barraOperacion = new Panel { Dock = DockStyle.Bottom, Height = Tema.S(52), BackColor = Color.White, Visible = false, Padding = new Padding(Tema.S(14), Tema.S(8), Tema.S(14), Tema.S(8)) };
        private readonly Label lblOperacion = new Label { Dock = DockStyle.Top, Height = Tema.S(18), Font = Tema.Chica, ForeColor = Tema.Texto, AutoEllipsis = true };
        private readonly ProgressBar progreso = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
        private readonly Button btnCancelar;

        private CancellationTokenSource cancelacion;
        private string operacionActual;
        private bool cerrarAlTerminar;
        private bool primeraRevisionProgramada = true;
        private readonly System.Windows.Forms.Timer relojSesion = new System.Windows.Forms.Timer { Interval = 5000 };
        private readonly System.Windows.Forms.Timer relojProgramador = new System.Windows.Forms.Timer { Interval = 60000 };

        public bool Ocupado { get { return operacionActual != null; } }

        public VentanaPrincipal(RebithService servicio)
        {
            Servicio = servicio;
            Sesion = new Sesion(servicio);
            Text = "Rebith - Gestor de respaldos";
            Font = Tema.Normal;
            BackColor = Tema.Fondo;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(Tema.S(1000), Tema.S(640));
            Size = new Size(Tema.S(1200), Tema.S(760));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            btnSesion = Tema.Boton("Entrar", (s, e) => AlternarSesion(), true);
            btnContrasena = Tema.Boton("Cambiar contrasena", (s, e) => CambiarContrasena());
            btnCancelar = Tema.Boton("Cancelar", (s, e) => CancelarOperacion());
            btnCancelar.Dock = DockStyle.Right;

            Controls.Add(contenido);
            Controls.Add(CrearEncabezado());
            Controls.Add(CrearMenu());
            barraOperacion.Controls.Add(progreso);
            barraOperacion.Controls.Add(new Panel { Dock = DockStyle.Right, Width = Tema.S(12) });
            barraOperacion.Controls.Add(btnCancelar);
            barraOperacion.Controls.Add(lblOperacion);
            Controls.Add(barraOperacion);

            Sesion.Cambio += (s, e) => { ActualizarSesion(); RefrescarActual(); };
            relojSesion.Tick += (s, e) => Sesion.RevisarInactividad(Ocupado);
            relojProgramador.Tick += (s, e) => RevisarProgramados();
            relojSesion.Start();
            relojProgramador.Start();
            Shown += (s, e) =>
            {
                ActualizarSesion();
                Navegar(Paginas.Inicio);
                // Primera revision a los 5 segundos (incluye "ejecutar al iniciar").
                var inicio = new System.Windows.Forms.Timer { Interval = 5000 };
                inicio.Tick += (s2, e2) => { inicio.Stop(); inicio.Dispose(); RevisarProgramados(); };
                inicio.Start();
            };
            FormClosing += AlCerrar;
        }

        // ------------------------------------------------------------------ armado

        private Control CrearMenu()
        {
            var menu = new Panel { Dock = DockStyle.Left, Width = Tema.S(210), BackColor = Tema.Menu };
            var lista = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, Tema.S(8), 0, 0) };
            var marca = new Label
            {
                Text = "Rebith",
                Dock = DockStyle.Top,
                Height = Tema.S(70),
                Font = Tema.Grande,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter
            };
            AgregarOpcion(lista, Paginas.Inicio, "Inicio");
            AgregarOpcion(lista, Paginas.Objetivos, "Que respaldar");
            AgregarOpcion(lista, Paginas.Historial, "Historial y restaurar");
            AgregarOpcion(lista, Paginas.Verificar, "Verificar");
            AgregarOpcion(lista, Paginas.Configuracion, "Configuracion");
            AgregarOpcion(lista, Paginas.Administracion, "Usuarios y auditoria");
            var version = new Label
            {
                Text = "v" + Application.ProductVersion,
                Dock = DockStyle.Bottom,
                Height = Tema.S(28),
                Font = Tema.Chica,
                ForeColor = Color.FromArgb(160, 180, 210),
                TextAlign = ContentAlignment.MiddleCenter
            };
            menu.Controls.Add(lista);
            menu.Controls.Add(version);
            menu.Controls.Add(marca);
            return menu;
        }

        private void AgregarOpcion(FlowLayoutPanel lista, Paginas pagina, string texto)
        {
            var b = new Button
            {
                Text = "   " + texto,
                Width = Tema.S(210),
                Height = Tema.S(46),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Tema.Menu,
                Font = Tema.Normal,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 70, 115);
            b.Click += (s, e) => Navegar(pagina);
            botonesMenu[pagina] = b;
            lista.Controls.Add(b);
        }

        private Control CrearEncabezado()
        {
            var barra = new Panel { Dock = DockStyle.Top, Height = Tema.S(56), BackColor = Color.White, Padding = new Padding(Tema.S(16), Tema.S(8), Tema.S(16), Tema.S(4)) };
            var derecha = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
            derecha.Controls.Add(lblUsuario);
            derecha.Controls.Add(btnContrasena);
            derecha.Controls.Add(btnSesion);
            var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            lblDeposito.Text = "Deposito: " + Servicio.Repo.Raiz;
            izquierda.Controls.Add(lblDeposito);
            barra.Controls.Add(izquierda);
            barra.Controls.Add(derecha);
            barra.Paint += (s, e) =>
            {
                using (var pen = new Pen(Tema.Borde)) e.Graphics.DrawLine(pen, 0, barra.Height - 1, barra.Width, barra.Height - 1);
            };
            return barra;
        }

        // ------------------------------------------------------------------ navegacion

        public void Navegar(Paginas pagina)
        {
            try
            {
                Control c;
                if (!paginas.TryGetValue(pagina, out c))
                {
                    c = CrearPagina(pagina);
                    c.Dock = DockStyle.Fill;
                    paginas[pagina] = c;
                }
                contenido.SuspendLayout();
                contenido.Controls.Clear();
                contenido.Controls.Add(c);
                contenido.ResumeLayout();
                actual = pagina;
                foreach (var par in botonesMenu)
                {
                    par.Value.BackColor = par.Key == pagina ? Tema.MenuActivo : Tema.Menu;
                    par.Value.Font = par.Key == pagina ? Tema.Negrita : Tema.Normal;
                }
                RefrescarActual();
            }
            catch (Exception ex)
            {
                Program.ErrorInesperado(ex);
            }
        }

        private Control CrearPagina(Paginas pagina)
        {
            switch (pagina)
            {
                case Paginas.Objetivos: return new PaginaObjetivos(this);
                case Paginas.Historial: return new PaginaHistorial(this);
                case Paginas.Verificar: return new PaginaVerificar(this);
                case Paginas.Configuracion: return new PaginaConfiguracion(this);
                case Paginas.Administracion: return new PaginaAdministracion(this);
                default: return new PaginaInicio(this);
            }
        }

        public T Pagina<T>(Paginas pagina) where T : Control
        {
            Navegar(pagina);
            return paginas[pagina] as T;
        }

        public void RefrescarActual()
        {
            Control c;
            if (!paginas.TryGetValue(actual, out c)) return;
            var p = c as IPagina;
            if (p == null) return;
            try
            {
                p.Refrescar();
            }
            catch (Exception ex)
            {
                Log.Error("Refrescar " + actual, ex);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Mismo atajo que la version en Python para el acceso especial.
            if (keyData == (Keys.Control | Keys.Alt | Keys.F))
            {
                Navegar(Paginas.Administracion);
                if (!Sesion.Activa) Sesion.Entrar(this, "Acceso especial: entre con un usuario administrador.");
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ sesion

        private void AlternarSesion()
        {
            if (Sesion.Activa) Sesion.Salir("cerro sesion");
            else Sesion.Entrar(this, null);
        }

        private void CambiarContrasena()
        {
            if (!Sesion.Activa) return;
            using (var d = new DialogoCambiarContrasena(Servicio, Sesion.Nombre, ""))
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                    MessageBox.Show(this, "Contrasena actualizada.", "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ActualizarSesion()
        {
            if (Sesion.Activa)
            {
                lblUsuario.Text = Sesion.Nombre + " (" + Sesion.Usuario.Role + ")";
                btnSesion.Text = "Salir";
                btnContrasena.Visible = true;
            }
            else
            {
                lblUsuario.Text = "Sin sesion (solo consulta)";
                btnSesion.Text = "Entrar";
                btnContrasena.Visible = false;
            }
        }

        // ------------------------------------------------------------------ operaciones

        /// <summary>
        /// Corre un trabajo largo sin congelar la ventana, con barra de progreso y boton
        /// Cancelar. Solo una a la vez. Cualquier error se muestra con un mensaje claro y
        /// la ventana sigue funcionando.
        /// </summary>
        public bool Ejecutar<T>(string titulo, Func<IProgress<ProgressInfo>, CancellationToken, T> trabajo,
            Action<T> alTerminar, bool silencioso = false)
        {
            if (Ocupado)
            {
                if (!silencioso)
                    MessageBox.Show(this, "Espere a que termine: " + operacionActual + ".", "Operacion en curso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            operacionActual = titulo;
            cancelacion = new CancellationTokenSource();
            CancellationToken token = cancelacion.Token;
            lblOperacion.Text = titulo + "...";
            progreso.Style = ProgressBarStyle.Marquee;
            progreso.Value = 0;
            btnCancelar.Enabled = true;
            barraOperacion.Visible = true;

            DateTime ultimo = DateTime.MinValue;
            var reporte = new Progress<ProgressInfo>(p =>
            {
                if ((DateTime.Now - ultimo).TotalMilliseconds < 100 && p.Current != p.Total) return;
                ultimo = DateTime.Now;
                if (p.Total > 0)
                {
                    progreso.Style = ProgressBarStyle.Continuous;
                    progreso.Maximum = p.Total;
                    progreso.Value = Math.Max(0, Math.Min(p.Total, p.Current));
                    lblOperacion.Text = string.Format("{0}: {1} ({2} de {3})  {4}", titulo, p.Stage, p.Current, p.Total, p.Item);
                }
                else
                {
                    progreso.Style = ProgressBarStyle.Marquee;
                    lblOperacion.Text = titulo + ": " + p.Stage;
                }
            });

            Task<T> tarea = Task.Run(() => trabajo(reporte, token));
            tarea.ContinueWith(t =>
            {
                operacionActual = null;
                barraOperacion.Visible = false;
                if (cancelacion != null) { cancelacion.Dispose(); cancelacion = null; }
                try
                {
                    if (t.IsFaulted)
                        MostrarError(titulo, t.Exception.GetBaseException(), silencioso);
                    else if (t.IsCanceled)
                    {
                        if (!silencioso) MessageBox.Show(this, titulo + ": cancelado.", "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (alTerminar != null)
                        alTerminar(t.Result);
                }
                catch (Exception ex)
                {
                    Program.ErrorInesperado(ex);
                }
                RefrescarActual();
                if (cerrarAlTerminar) Close();
            }, TaskScheduler.FromCurrentSynchronizationContext());
            return true;
        }

        private void MostrarError(string titulo, Exception ex, bool silencioso)
        {
            if (ex is OperationCanceledException)
            {
                if (!silencioso) MessageBox.Show(this, titulo + ": cancelado. No quedo nada a medias.", "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ex is RebithException)
            {
                Log.Advertencia(titulo + ": " + ex.Message);
                if (!silencioso) MessageBox.Show(this, ex.Message, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Log.Error(titulo, ex);
            if (!silencioso)
                MessageBox.Show(this, "No se pudo completar: " + ex.Message + "\n\nNo quedo ningun respaldo a medias. El detalle quedo en el log.",
                    titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void CancelarOperacion()
        {
            if (cancelacion == null) return;
            btnCancelar.Enabled = false;
            lblOperacion.Text = operacionActual + ": cancelando (se deja todo en orden)...";
            cancelacion.Cancel();
        }

        private void AlCerrar(object sender, FormClosingEventArgs e)
        {
            if (!Ocupado) return;
            DialogResult r = MessageBox.Show(this, "Hay una operacion en curso: " + operacionActual +
                ".\n\nSi = cancelarla y cerrar (no queda nada a medias)\nNo = seguir esperando", "Rebith",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            e.Cancel = true;
            if (r == DialogResult.Yes)
            {
                cerrarAlTerminar = true;
                CancelarOperacion();
            }
        }

        // ------------------------------------------------------------------ programador

        /// <summary>
        /// Con la ventana abierta tambien corren los respaldos programados. (El Programador de
        /// tareas de Windows los corre aunque la ventana este cerrada.)
        /// </summary>
        private void RevisarProgramados()
        {
            if (Ocupado) return;
            bool alIniciar = primeraRevisionProgramada;
            bool toca;
            try
            {
                DateTime ahora = DateTime.Now;
                List<TargetInfo> objetivos = Servicio.Repo.LeerObjetivos();
                toca = objetivos.Any(t => Programador.TocaAhora(t, ahora)) ||
                       Programador.TocaRevisionAutomatica(Servicio.Repo.LeerAjustes(), ahora) ||
                       (alIniciar && objetivos.Any(t => t.Schedule.RunOnStartup && t.Schedule.Frequency != Frecuencias.Manual));
            }
            catch (Exception ex)
            {
                Log.Error("Revisar programados", ex);
                return;
            }
            primeraRevisionProgramada = false;
            if (!toca) return;
            Ejecutar("Respaldo programado", (p, t) => Servicio.EjecutarPendientes(alIniciar, t), r =>
            {
                if (r.Lineas.Count > 0) Log.Info("Programado desde la ventana: " + string.Join(" | ", r.Lineas));
            }, true);
        }
    }
}
