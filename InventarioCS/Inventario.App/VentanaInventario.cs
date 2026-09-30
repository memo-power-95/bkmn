using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;
using Inventario.Core;

namespace Inventario.App
{
    /// <summary>
    /// Pestaña "Inventario (Racks)" de inv.py como ventana propia: elegir rack y tray,
    /// escanear Lot ID (con deteccion de duplicados en todo el rack), deshacer, ver QTY
    /// por tray y exportar a Excel.
    /// </summary>
    public class VentanaInventario : Form
    {
        private readonly BaseInventario db;
        private Rack rackActual;

        private readonly ComboBox comboRack;
        private readonly NumericUpDown numTray;
        private readonly Label lblDeTrays;
        private readonly TextBox txtLot;
        private readonly Banner banner;
        private readonly Label lblResumen;
        private readonly DataGridView tabla;

        public VentanaInventario(BaseInventario db)
        {
            this.db = db;
            Text = "Inventario por Racks";
            BackColor = Tema.Fondo;
            Font = Tema.Normal;
            ClientSize = new Size(Tema.S(820), Tema.S(700));
            MinimumSize = new Size(Tema.S(640), Tema.S(480));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            var raiz = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(Tema.S(14)),
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // --- rack y tray ---
            FlowLayoutPanel filaRack = Tema.Fila();
            comboRack = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Tema.S(280), Font = Tema.Normal, Anchor = AnchorStyles.Left };
            comboRack.SelectedIndexChanged += (s, e) => SeleccionarRack(comboRack.SelectedItem as Rack);
            numTray = new NumericUpDown { Minimum = 1, Maximum = 1, Width = Tema.S(80), Font = Tema.Negrita, Anchor = AnchorStyles.Left };
            numTray.ValueChanged += (s, e) => MarcarTrayEnTabla();
            lblDeTrays = Tema.Etiqueta("", Tema.Chica, Tema.TextoSuave);
            filaRack.Controls.Add(Tema.Etiqueta("Rack:", Tema.Negrita));
            filaRack.Controls.Add(comboRack);
            filaRack.Controls.Add(Tema.Boton("Nuevo rack", (s, e) => NuevoRack()));
            filaRack.Controls.Add(Tema.Etiqueta("   Tray actual:", Tema.Negrita));
            filaRack.Controls.Add(numTray);
            filaRack.Controls.Add(lblDeTrays);

            // --- escaneo ---
            FlowLayoutPanel filaEscaneo = Tema.Fila();
            txtLot = new TextBox { Width = Tema.S(360), Font = Tema.Escaneo, Anchor = AnchorStyles.Left };
            txtLot.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;   // sin el "ding" de Windows
                Escanear();
            };
            filaEscaneo.Controls.Add(Tema.Etiqueta("Escanear Lot ID:", Tema.Negrita));
            filaEscaneo.Controls.Add(txtLot);
            filaEscaneo.Controls.Add(Tema.Boton("Escanear", (s, e) => Escanear(), true));
            filaEscaneo.Controls.Add(Tema.Boton("Deshacer último", (s, e) => Deshacer()));

            banner = new Banner { Margin = new Padding(0, Tema.S(4), 0, Tema.S(4)) };

            // --- resumen ---
            lblResumen = Tema.Etiqueta("Resumen del rack (QTY por tray)", Tema.Negrita);
            lblResumen.Margin = new Padding(0, Tema.S(8), 0, Tema.S(4));
            tabla = Tema.Tabla();
            tabla.Columns.Add(Tema.Columna("tray", "# Tray", 50));
            tabla.Columns.Add(Tema.Columna("qty", "QTY", 50));
            // Clic en un tray = ese tray pasa a ser el actual.
            tabla.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                numTray.Value = Math.Min(numTray.Maximum, e.RowIndex + 1);
                txtLot.Focus();
            };

            // --- exportar ---
            FlowLayoutPanel filaExportar = Tema.Fila();
            filaExportar.Controls.Add(Tema.Boton("Exportar ESTE rack a Excel", (s, e) => Exportar(false)));
            filaExportar.Controls.Add(Tema.Boton("Exportar TODOS los racks a Excel", (s, e) => Exportar(true)));
            Label lblBase = Tema.Etiqueta("Base: " + db.Ruta, Tema.Chica, Tema.TextoSuave);

            Agregar(raiz, filaRack, false);
            Agregar(raiz, filaEscaneo, false);
            Agregar(raiz, banner, false);
            Agregar(raiz, lblResumen, false);
            Agregar(raiz, tabla, true);
            Agregar(raiz, filaExportar, false);
            Agregar(raiz, lblBase, false);
            Controls.Add(raiz);

            // Atajos: F2 enfoca el escaneo, Ctrl+Z deshace.
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2) { txtLot.Focus(); e.Handled = true; }
                else if (e.Control && e.KeyCode == Keys.Z && !txtLot.Focused) { Deshacer(); e.Handled = true; }
            };

            Shown += (s, e) =>
            {
                RefrescarRacks(null);
                txtLot.Focus();
            };
        }

        private static void Agregar(TableLayoutPanel raiz, Control c, bool llenar)
        {
            raiz.RowStyles.Add(llenar ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            c.Dock = DockStyle.Fill;
            raiz.Controls.Add(c, 0, raiz.RowStyles.Count - 1);
        }

        // ------------------------------------------------------------------ racks

        private void RefrescarRacks(long? seleccionar)
        {
            List<Rack> racks = db.ListarRacks();
            comboRack.BeginUpdate();
            comboRack.Items.Clear();
            foreach (Rack r in racks) comboRack.Items.Add(r);
            comboRack.EndUpdate();

            Rack elegido = racks.FirstOrDefault(r => r.Id == (seleccionar ?? (rackActual != null ? rackActual.Id : -1)))
                           ?? racks.FirstOrDefault();
            if (elegido != null) comboRack.SelectedItem = elegido;   // dispara SeleccionarRack
            else SeleccionarRack(null);
        }

        private void SeleccionarRack(Rack rack)
        {
            rackActual = rack;
            if (rack == null)
            {
                numTray.Maximum = 1;
                lblDeTrays.Text = "";
                banner.Mostrar("Crea un rack con \"Nuevo rack\" para empezar.", Tema.NeutroFondo, Tema.Texto);
            }
            else
            {
                numTray.Value = 1;
                numTray.Maximum = rack.NumTrays;
                lblDeTrays.Text = "de " + rack.NumTrays;
                banner.Mostrar("", Tema.Fondo, Tema.Texto);
            }
            RefrescarResumen();
            txtLot.Focus();
        }

        private void NuevoRack()
        {
            using (var d = new DialogoNuevoRack(db))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                RefrescarRacks(d.RackCreado);
            }
        }

        // ------------------------------------------------------------------ escaneo

        private void Escanear()
        {
            string lotId = txtLot.Text.Trim();
            txtLot.Clear();
            txtLot.Focus();
            if (lotId.Length == 0) return;
            if (rackActual == null)
            {
                MessageBox.Show(this, "Selecciona o crea un rack primero.", "Sin rack", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int tray = (int)numTray.Value;

            // deteccion de duplicados en TODO el rack (misma logica que Python)
            int? existente = db.Escanear(rackActual.Id, tray, lotId);
            if (existente != null)
            {
                banner.Mostrar("⚠ " + lotId + " ya está en este rack (tray " + existente + ").", Tema.MalFondo, Tema.Mal);
                SystemSounds.Hand.Play();
                return;
            }
            banner.Mostrar("✔ " + lotId + " agregado al tray " + tray + ".", Tema.BienFondo, Tema.Bien);
            RefrescarResumen();
        }

        private void Deshacer()
        {
            if (rackActual == null) return;
            int tray = (int)numTray.Value;
            string lotId = db.DeshacerUltimo(rackActual.Id, tray);
            if (lotId != null)
            {
                banner.Mostrar("↩ Deshecho: " + lotId + " (tray " + tray + ").", Tema.NeutroFondo, Tema.Texto);
                RefrescarResumen();
            }
            else
            {
                banner.Mostrar("No hay nada que deshacer en este tray.", Tema.MalFondo, Tema.Mal);
            }
            txtLot.Focus();
        }

        private void RefrescarResumen()
        {
            tabla.Rows.Clear();
            if (rackActual == null)
            {
                lblResumen.Text = "Resumen del rack (QTY por tray)";
                return;
            }
            Dictionary<int, int> qtys = db.QtyPorTray(rackActual.Id);
            var filas = new DataGridViewRow[rackActual.NumTrays];
            for (int t = 1; t <= rackActual.NumTrays; t++)
            {
                int qty;
                qtys.TryGetValue(t, out qty);
                var fila = new DataGridViewRow();
                fila.CreateCells(tabla, t, qty);
                filas[t - 1] = fila;
            }
            tabla.Rows.AddRange(filas);
            lblResumen.Text = "Resumen de " + rackActual.Nombre + " (QTY por tray) — Total: " + qtys.Values.Sum() + " piezas";
            MarcarTrayEnTabla();
        }

        private void MarcarTrayEnTabla()
        {
            int i = (int)numTray.Value - 1;
            if (i < 0 || i >= tabla.Rows.Count) return;
            tabla.ClearSelection();
            tabla.Rows[i].Selected = true;
            if (!tabla.Rows[i].Displayed) tabla.FirstDisplayedScrollingRowIndex = i;
        }

        // ------------------------------------------------------------------ exportar

        private void Exportar(bool todos)
        {
            List<RackExportado> racks;
            if (todos)
            {
                racks = db.ExportarTodos().Where(r => r.TotalQty > 0).ToList();
                if (racks.Count == 0)
                {
                    MessageBox.Show(this, "No hay racks con piezas escaneadas.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            else
            {
                if (rackActual == null)
                {
                    MessageBox.Show(this, "Selecciona un rack primero.", "Sin rack", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                RackExportado r = db.ExportarRack(rackActual.Id);
                if (r == null || r.TotalQty == 0)
                {
                    MessageBox.Show(this, "Este rack no tiene piezas escaneadas.", "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                racks = new List<RackExportado> { r };
            }

            string ruta;
            using (var d = new SaveFileDialog
            {
                Filter = "Libro de Excel (*.xlsx)|*.xlsx",
                FileName = "inventario_racks_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ruta = d.FileName;
            }

            try
            {
                ExcelInventario.Escribir(ruta, racks);
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "No se pudo guardar el archivo (¿está abierto en Excel?).\n\n" + ex.Message,
                    "Exportar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show(this, "Archivo guardado como:\n" + ruta + "\n\n¿Abrirlo ahora?", "Exportado",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Abrir", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }
    }
}
