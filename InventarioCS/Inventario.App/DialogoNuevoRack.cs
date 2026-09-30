using System;
using System.Drawing;
using System.Windows.Forms;
using Inventario.Core;

namespace Inventario.App
{
    /// <summary>Nombre automatico (R1, R2...) o manual, y numero de trays (1 a 500).</summary>
    public class DialogoNuevoRack : Form
    {
        private readonly BaseInventario db;
        private readonly CheckBox chkAuto;
        private readonly TextBox txtNombre;
        private readonly Label lblVista;
        private readonly NumericUpDown numTrays;

        public long RackCreado { get; private set; }

        public DialogoNuevoRack(BaseInventario db)
        {
            this.db = db;
            Text = "Nuevo rack";
            Font = Tema.Normal;
            BackColor = Tema.Fondo;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var t = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(Tema.S(16)),
                Dock = DockStyle.Fill
            };

            chkAuto = new CheckBox { Text = "Nombrar automáticamente (R1, R2, R3...)", Checked = true, AutoSize = true };
            txtNombre = new TextBox { Width = Tema.S(260), Enabled = false };
            lblVista = Tema.Etiqueta("", Tema.Negrita, Tema.Bien);
            numTrays = new NumericUpDown { Minimum = 1, Maximum = BaseInventario.MaxTraysPorRack, Value = 6, Width = Tema.S(90) };
            chkAuto.CheckedChanged += (s, e) => ActualizarVista();

            var botones = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, Tema.S(12), 0, 0) };
            Button crear = Tema.Boton("Crear", (s, e) => Crear(), true);
            Button cancelar = Tema.Boton("Cancelar", null);
            cancelar.DialogResult = DialogResult.Cancel;
            botones.Controls.Add(crear);
            botones.Controls.Add(cancelar);

            t.Controls.Add(chkAuto);
            t.Controls.Add(lblVista);
            t.Controls.Add(Tema.Etiqueta("Nombre del rack:"));
            t.Controls.Add(txtNombre);
            t.Controls.Add(Tema.Etiqueta("Número de trays en este rack (1 a " + BaseInventario.MaxTraysPorRack + "):"));
            t.Controls.Add(numTrays);
            t.Controls.Add(botones);
            Controls.Add(t);

            AcceptButton = crear;
            CancelButton = cancelar;
            ActualizarVista();
        }

        private void ActualizarVista()
        {
            txtNombre.Enabled = !chkAuto.Checked;
            lblVista.Visible = chkAuto.Checked;
            if (chkAuto.Checked) lblVista.Text = "Se nombrará: " + db.SiguienteNombreRackAutomatico();
            else txtNombre.Focus();
        }

        private void Crear()
        {
            string nombre = chkAuto.Checked ? db.SiguienteNombreRackAutomatico() : txtNombre.Text.Trim();
            if (nombre.Length == 0)
            {
                MessageBox.Show(this, "Ingresa un nombre para el rack.", "Falta nombre", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                RackCreado = db.CrearRack(nombre, (int)numTrays.Value);
            }
            catch (InventarioException ex)
            {
                MessageBox.Show(this, ex.Message, "No se creó el rack", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
