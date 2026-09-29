using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Inventario.App
{
    /// <summary>Colores, letras y controles con el mismo estilo que Rebith.</summary>
    public static class Tema
    {
        public static readonly Color Fondo = Color.FromArgb(245, 247, 250);
        public static readonly Color Acento = Color.FromArgb(37, 99, 235);
        public static readonly Color Texto = Color.FromArgb(31, 41, 55);
        public static readonly Color TextoSuave = Color.FromArgb(107, 114, 128);
        public static readonly Color Borde = Color.FromArgb(220, 224, 230);
        public static readonly Color Bien = Color.FromArgb(21, 128, 61);
        public static readonly Color BienFondo = Color.FromArgb(220, 252, 231);
        public static readonly Color Mal = Color.FromArgb(185, 28, 28);
        public static readonly Color MalFondo = Color.FromArgb(254, 226, 226);
        public static readonly Color NeutroFondo = Color.FromArgb(243, 244, 246);
        public static readonly Color QtyFondo = Color.FromArgb(146, 208, 80);   // mismo verde del Excel

        public static readonly Font Normal = new Font("Segoe UI", 10F);
        public static readonly Font Negrita = new Font("Segoe UI", 10F, FontStyle.Bold);
        public static readonly Font Chica = new Font("Segoe UI", 9F);
        public static readonly Font Escaneo = new Font("Consolas", 14F);
        public static readonly Font Grande = new Font("Segoe UI Semibold", 20F);

        private static float escala = 1f;

        public static void Iniciar()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) escala = g.DpiX / 96f;
            }
            catch
            {
                escala = 1f;
            }
        }

        /// <summary>Pixeles ajustados a la escala de la pantalla (125%, 150%...).</summary>
        public static int S(int pixeles)
        {
            return (int)Math.Round(pixeles * escala);
        }

        public static Button Boton(string texto, EventHandler click, bool primario = false)
        {
            var b = new Button
            {
                Text = texto,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(S(110), S(36)),
                Padding = new Padding(S(10), S(2), S(10), S(2)),
                Margin = new Padding(0, 0, S(8), S(6)),
                FlatStyle = FlatStyle.Flat,
                Font = primario ? Negrita : Normal,
                BackColor = primario ? Acento : Color.White,
                ForeColor = primario ? Color.White : Texto,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = primario ? Acento : Borde;
            b.FlatAppearance.BorderSize = 1;
            if (click != null) b.Click += click;
            return b;
        }

        public static Label Etiqueta(string texto, Font fuente = null, Color? color = null)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                Font = fuente ?? Normal,
                ForeColor = color ?? Texto,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, S(4), S(8), S(4))
            };
        }

        public static FlowLayoutPanel Fila()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, S(6), 0, S(4))
            };
        }

        public static DataGridView Tabla()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Borde,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = S(34),
                EnableHeadersVisualStyles = false,
                Font = Normal
            };
            g.RowTemplate.Height = S(28);
            g.ColumnHeadersDefaultCellStyle.BackColor = NeutroFondo;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Texto;
            g.ColumnHeadersDefaultCellStyle.Font = Negrita;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            g.DefaultCellStyle.SelectionForeColor = Texto;
            g.DefaultCellStyle.Padding = new Padding(S(4), 0, S(4), 0);
            DobleBuffer(g);
            // Una celda con un valor raro nunca debe tumbar la ventana.
            g.DataError += (s, e) => { e.ThrowException = false; };
            return g;
        }

        public static DataGridViewTextBoxColumn Columna(string nombre, string titulo, int peso)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = nombre,
                HeaderText = titulo,
                FillWeight = peso,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        public static void DobleBuffer(Control c)
        {
            try
            {
                PropertyInfo p = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null) p.SetValue(c, true, null);
            }
            catch
            {
            }
        }
    }

    /// <summary>Franja de color con el resultado del ultimo escaneo.</summary>
    public class Banner : Label
    {
        public Banner()
        {
            Dock = DockStyle.Top;
            AutoSize = false;
            Height = Tema.S(44);
            Padding = new Padding(Tema.S(12), 0, Tema.S(12), 0);
            TextAlign = ContentAlignment.MiddleLeft;
            Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            Visible = false;
        }

        public void Mostrar(string texto, Color fondo, Color color)
        {
            Text = texto;
            BackColor = fondo;
            ForeColor = color;
            Visible = !string.IsNullOrEmpty(texto);
        }
    }
}
