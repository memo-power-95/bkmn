using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    /// <summary>Ventana de dialogo con el estilo de la aplicacion.</summary>
    public class DialogoBase : Form
    {
        protected readonly TableLayoutPanel Cuerpo;
        protected readonly FlowLayoutPanel Botones;
        private readonly int anchoCuerpo;

        public DialogoBase(string titulo, int ancho)
        {
            Text = titulo;
            Font = Tema.Normal;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Tema.S(18));

            anchoCuerpo = Tema.S(ancho);
            // Todo con AutoSize y sin Dock: la ventana crece lo justo para su contenido.
            var raiz = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2, Location = new Point(Tema.S(18), Tema.S(18)) };
            Cuerpo = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
            Cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Tema.S(130)));
            Cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, anchoCuerpo - Tema.S(130)));
            Botones = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, Anchor = AnchorStyles.Right, Padding = new Padding(0, Tema.S(12), 0, 0) };
            raiz.Controls.Add(Cuerpo, 0, 0);
            raiz.Controls.Add(Botones, 0, 1);
            Controls.Add(raiz);
        }

        protected void AgregarTexto(string texto, Color? color = null)
        {
            Label l = Tema.Etiqueta(texto, Tema.Normal, color);
            l.MaximumSize = new Size(anchoCuerpo, 0);
            Cuerpo.Controls.Add(l, 0, Cuerpo.RowCount);
            Cuerpo.SetColumnSpan(l, 2);
            Cuerpo.RowCount++;
        }

        protected T AgregarCampo<T>(string etiqueta, T control) where T : Control
        {
            Label l = Tema.Etiqueta(etiqueta);
            l.Anchor = AnchorStyles.Left;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            control.Margin = new Padding(0, Tema.S(4), 0, Tema.S(4));
            Cuerpo.Controls.Add(l, 0, Cuerpo.RowCount);
            Cuerpo.Controls.Add(control, 1, Cuerpo.RowCount);
            Cuerpo.RowCount++;
            return control;
        }

        protected Button AgregarBoton(string texto, DialogResult resultado, bool primario)
        {
            Button b = Tema.Boton(texto, null, primario);
            b.DialogResult = resultado;
            Botones.Controls.Add(b);
            if (primario) AcceptButton = b;
            if (resultado == DialogResult.Cancel) CancelButton = b;
            return b;
        }

        protected void Error(string texto)
        {
            MessageBox.Show(this, texto, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public class DialogoLogin : DialogoBase
    {
        private readonly RebithService servicio;
        private readonly TextBox usuario = new TextBox();
        private readonly TextBox contrasena = new TextBox { UseSystemPasswordChar = true };
        public UserInfo Resultado { get; private set; }

        public DialogoLogin(RebithService servicio, string motivo) : base("Entrar", 420)
        {
            this.servicio = servicio;
            if (!string.IsNullOrEmpty(motivo)) AgregarTexto(motivo, Tema.TextoSuave);
            AgregarCampo("Usuario", usuario);
            AgregarCampo("Contrasena", contrasena);
            Button entrar = AgregarBoton("Entrar", DialogResult.None, true);
            AgregarBoton("Cancelar", DialogResult.Cancel, false);
            entrar.Click += Entrar;
            Shown += (s, e) => usuario.Focus();
        }

        private void Entrar(object sender, EventArgs e)
        {
            ResultadoLogin r;
            try
            {
                r = servicio.Usuarios.Entrar(usuario.Text, contrasena.Text);
            }
            catch (Exception ex)
            {
                Error("No se pudo validar el usuario: " + ex.Message);
                return;
            }
            if (r.Usuario == null)
            {
                Error(r.Error);
                contrasena.SelectAll();
                contrasena.Focus();
                return;
            }
            if (r.DebeCambiarContrasena)
            {
                MessageBox.Show(this, "Debe cambiar su contrasena antes de continuar.", "Cambiar contrasena", MessageBoxButtons.OK, MessageBoxIcon.Information);
                using (var d = new DialogoCambiarContrasena(servicio, r.Usuario.Username, contrasena.Text))
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                }
                r.Usuario = servicio.Usuarios.Refrescar(r.Usuario.Username);
            }
            Resultado = r.Usuario;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public class DialogoCambiarContrasena : DialogoBase
    {
        private readonly RebithService servicio;
        private readonly string usuario;
        private readonly TextBox actual = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox nueva = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox repetir = new TextBox { UseSystemPasswordChar = true };

        public DialogoCambiarContrasena(RebithService servicio, string usuario, string actualConocida) : base("Cambiar contrasena", 440)
        {
            this.servicio = servicio;
            this.usuario = usuario;
            AgregarTexto("Usuario: " + usuario + ". Minimo 6 caracteres.", Tema.TextoSuave);
            AgregarCampo("Actual", actual).Text = actualConocida ?? "";
            AgregarCampo("Nueva", nueva);
            AgregarCampo("Repetir nueva", repetir);
            Button guardar = AgregarBoton("Guardar", DialogResult.None, true);
            AgregarBoton("Cancelar", DialogResult.Cancel, false);
            guardar.Click += Guardar;
            Shown += (s, e) => (actual.Text.Length > 0 ? nueva : actual).Focus();
        }

        private void Guardar(object sender, EventArgs e)
        {
            if (nueva.Text != repetir.Text)
            {
                Error("Las contrasenas nuevas no coinciden.");
                return;
            }
            try
            {
                servicio.Usuarios.CambiarMiContrasena(usuario, actual.Text, nueva.Text);
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

    /// <summary>Pide un texto o un numero.</summary>
    public class DialogoPedir : DialogoBase
    {
        private readonly TextBox texto;
        private readonly NumericUpDown numero;

        private DialogoPedir(string titulo, string pregunta) : base(titulo, 440)
        {
            AgregarTexto(pregunta);
        }

        public DialogoPedir(string titulo, string pregunta, string etiqueta, string valor) : this(titulo, pregunta)
        {
            texto = AgregarCampo(etiqueta, new TextBox { Text = valor ?? "" });
            AgregarBoton("Aceptar", DialogResult.OK, true);
            AgregarBoton("Cancelar", DialogResult.Cancel, false);
        }

        public DialogoPedir(string titulo, string pregunta, string etiqueta, int valor, int minimo, int maximo) : this(titulo, pregunta)
        {
            numero = AgregarCampo(etiqueta, new NumericUpDown { Minimum = minimo, Maximum = maximo, Value = Math.Max(minimo, Math.Min(maximo, valor)) });
            AgregarBoton("Aceptar", DialogResult.OK, true);
            AgregarBoton("Cancelar", DialogResult.Cancel, false);
        }

        public string Texto { get { return texto == null ? "" : texto.Text; } }
        public int Numero { get { return numero == null ? 0 : (int)numero.Value; } }
    }

    /// <summary>Muestra una lista de lineas (resultado de una operacion), con opcion de copiar.</summary>
    public class DialogoLista : Form
    {
        public DialogoLista(string titulo, string resumen, IEnumerable<string> lineas, Color colorResumen)
        {
            Text = titulo;
            Font = Tema.Normal;
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(Tema.S(760), Tema.S(480));
            MinimizeBox = false;
            ShowInTaskbar = false;
            Padding = new Padding(Tema.S(14));

            var encabezado = new Label { Text = resumen, Dock = DockStyle.Top, Height = Tema.S(56), Font = Tema.Subtitulo, ForeColor = colorResumen };
            var lista = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, BackColor = Color.White, Font = new Font("Consolas", 9.5F) };
            lista.Lines = new List<string>(lineas).ToArray();
            var abajo = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, Tema.S(8), 0, 0) };
            Button cerrar = Tema.Boton("Cerrar", (s, e) => Close(), true);
            Button copiar = Tema.Boton("Copiar", (s, e) =>
            {
                try { Clipboard.SetText(resumen + Environment.NewLine + lista.Text); } catch { }
            });
            abajo.Controls.Add(cerrar);
            abajo.Controls.Add(copiar);
            Controls.Add(lista);
            Controls.Add(abajo);
            Controls.Add(encabezado);
            AcceptButton = cerrar;
            CancelButton = cerrar;
        }
    }
}
