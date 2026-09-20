using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    public class PaginaVerificar : PaginaBase
    {
        private class Opcion
        {
            public string Id;
            public string Texto;
            public override string ToString() { return Texto; }
        }

        private readonly ComboBox cmbObjetivo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Tema.S(300) };
        private readonly ComboBox cmbRespaldo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Tema.S(300) };
        private readonly RadioButton rbOrigen = new RadioButton { Text = "Comparar el respaldo con los archivos de hoy", AutoSize = true, Checked = true };
        private readonly RadioButton rbIntegridad = new RadioButton { Text = "Revisar que el respaldo este sano (lo descomprime y comprueba cada huella SHA-256)", AutoSize = true };
        private readonly CheckBox chkExacta = new CheckBox { Text = "Exacta: calcular la huella de TODOS los archivos (mas lento, detecta cambios aunque la fecha no cambie)", AutoSize = true };
        private readonly CheckBox chkTodo = new CheckBox { Text = "Todo el deposito (todos los respaldos de todos los objetivos)", AutoSize = true };
        private readonly CheckBox chkReplica = new CheckBox { Text = "Reparar automaticamente desde la replica lo que este danado", AutoSize = true, Checked = true };
        private readonly CheckBox chkSoloProblemas = new CheckBox { Text = "Mostrar solo problemas", AutoSize = true, Checked = true };
        private readonly Banner resumen = new Banner();
        private readonly Label lblContadores = new Label { Dock = DockStyle.Top, Height = Tema.S(26), Font = Tema.Negrita, ForeColor = Tema.Texto };
        private readonly DataGridView grid = Tema.Tabla();
        private readonly Button btnReparar;
        private readonly Button btnRepararSel;
        private ResultadoVerificacion ultimo;
        private string ultimoRespaldoId;
        private List<ItemVerificacion> visibles = new List<ItemVerificacion>();

        public PaginaVerificar(VentanaPrincipal v) : base(v)
        {
            grid.VirtualMode = true;
            grid.MultiSelect = true;
            grid.Columns.AddRange(Tema.Columna("Estado", "Estado", 70), Tema.Columna("Archivo", "Archivo", 260),
                Tema.Columna("Detalle", "Detalle", 260), Tema.Columna("Respaldo", "Respaldo", 110));
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.CellValueNeeded += Valor;
            grid.CellFormatting += Formato;

            cmbObjetivo.SelectedIndexChanged += (s, e) => CargarRespaldos();
            rbOrigen.CheckedChanged += (s, e) => ActualizarOpciones();
            chkTodo.CheckedChanged += (s, e) => ActualizarOpciones();
            chkSoloProblemas.CheckedChanged += (s, e) => Mostrar();

            var seleccion = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            seleccion.Controls.Add(Tema.Etiqueta("Objetivo", Tema.Negrita), 0, 0);
            seleccion.Controls.Add(cmbObjetivo, 1, 0);
            seleccion.Controls.Add(Tema.Etiqueta("Respaldo", Tema.Negrita), 0, 1);
            seleccion.Controls.Add(cmbRespaldo, 1, 1);

            var modos = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, Tema.S(6), 0, 0) };
            modos.Controls.Add(rbOrigen);
            modos.Controls.Add(Sangria(chkExacta));
            modos.Controls.Add(rbIntegridad);
            modos.Controls.Add(Sangria(chkTodo));
            modos.Controls.Add(Sangria(chkReplica));

            var acciones = Tema.FilaBotones();
            acciones.Controls.Add(Tema.Boton("Verificar ahora", (s, e) => Verificar(), true));
            btnRepararSel = Tema.Boton("Recuperar seleccionados en el origen...", (s, e) => Reparar(true));
            btnReparar = Tema.Boton("Recuperar todos los faltantes y modificados...", (s, e) => Reparar(false));
            acciones.Controls.Add(btnRepararSel);
            acciones.Controls.Add(btnReparar);
            acciones.Controls.Add(Tema.Boton("Exportar a Excel (CSV)", (s, e) => Exportar()));
            acciones.Controls.Add(Tema.Boton("Abrir reportes", (s, e) => AbrirCarpeta(Servicio.Repo.CarpetaReportes)));
            acciones.Controls.Add(chkSoloProblemas);

            var arriba = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(Tema.S(14))
            };
            seleccion.Dock = DockStyle.None;
            seleccion.AutoSize = true;
            modos.Dock = DockStyle.None;
            acciones.Dock = DockStyle.None;
            arriba.Controls.Add(seleccion);
            arriba.Controls.Add(modos);
            arriba.Controls.Add(acciones);

            var resultados = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, Tema.S(10), 0, 0) };
            resultados.Controls.Add(grid);
            resultados.Controls.Add(lblContadores);
            resultados.Controls.Add(resumen);

            Controls.Add(resultados);
            Controls.Add(arriba);
            Controls.Add(Tema.Ayuda("Comparar: dice que archivos cambiaron, faltan o son nuevos desde el respaldo. Revisar que este sano: confirma que el respaldo se puede restaurar bit por bit. La revision completa tambien corre sola cada semana (Configuracion)."));
            Controls.Add(Tema.EncabezadoPagina("Verificar"));
            ActualizarOpciones();
        }

        private static Control Sangria(Control c)
        {
            c.Margin = new Padding(Tema.S(24), 0, 0, Tema.S(4));
            return c;
        }

        private void ActualizarOpciones()
        {
            chkExacta.Enabled = rbOrigen.Checked;
            chkTodo.Enabled = rbIntegridad.Checked;
            chkReplica.Enabled = rbIntegridad.Checked;
            bool porRespaldo = rbOrigen.Checked || !chkTodo.Checked;
            cmbObjetivo.Enabled = porRespaldo;
            cmbRespaldo.Enabled = porRespaldo;
            ActualizarBotones();
        }

        private void ActualizarBotones()
        {
            bool hayRecuperables = ultimo != null && ultimo.Items.Any(i => i.Estado == EstadosVerificacion.Falta || i.Estado == EstadosVerificacion.Modificado);
            btnReparar.Enabled = hayRecuperables;
            btnRepararSel.Enabled = hayRecuperables;
        }

        protected override void AlRefrescar()
        {
            Repository.LecturaManifiestos lectura = Servicio.Repo.LeerManifiestos();
            List<TargetInfo> objetivos = Servicio.Repo.LeerObjetivos();
            var opciones = objetivos.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).Select(t => new Opcion { Id = t.Id, Texto = t.Name }).ToList();
            foreach (var g in lectura.Manifiestos.Where(m => objetivos.All(t => t.Id != m.TargetId)).GroupBy(m => m.TargetId))
                opciones.Add(new Opcion { Id = g.Key, Texto = g.First().TargetName + " (objetivo quitado)" });
            string previo = cmbObjetivo.SelectedItem is Opcion ? ((Opcion)cmbObjetivo.SelectedItem).Id : null;
            cmbObjetivo.Items.Clear();
            foreach (Opcion o in opciones) cmbObjetivo.Items.Add(o);
            int i = opciones.FindIndex(o => o.Id == previo);
            if (opciones.Count > 0) cmbObjetivo.SelectedIndex = i >= 0 ? i : 0;
            else cmbRespaldo.Items.Clear();
        }

        private void CargarRespaldos()
        {
            var o = cmbObjetivo.SelectedItem as Opcion;
            cmbRespaldo.Items.Clear();
            if (o == null) return;
            foreach (SnapshotManifest m in Servicio.Repo.RespaldosDe(o.Id))
                cmbRespaldo.Items.Add(new Opcion { Id = m.Id, Texto = Tiempo.Legible(m.StartedLocal) + "  -  " + m.Files.Count + " archivos" + (m.Note.Length > 0 ? "  -  " + m.Note : "") });
            if (cmbRespaldo.Items.Count > 0) cmbRespaldo.SelectedIndex = 0;
        }

        private void Verificar()
        {
            bool integridad = rbIntegridad.Checked;
            bool todo = integridad && chkTodo.Checked;
            var respaldo = cmbRespaldo.SelectedItem as Opcion;
            if (!todo && respaldo == null)
            {
                Aviso("Elija un objetivo y un respaldo.");
                return;
            }
            string id = todo ? null : respaldo.Id;
            bool exacta = chkExacta.Checked;
            bool replica = chkReplica.Checked;
            string usuario = Sesion.ParaAuditoria;
            string titulo = integridad ? "Revisando que el respaldo este sano" : "Comparando con los archivos de hoy";
            Ventana.Ejecutar(titulo, (p, c) =>
            {
                ResultadoVerificacion r = integridad
                    ? Servicio.RevisarIntegridad(id, replica, 0, usuario, p, c)
                    : Servicio.CompararConOrigen(id, exacta, usuario, p, c);
                try
                {
                    Verifier.GuardarReporte(r, Servicio.Repo.CarpetaReportes, integridad ? "integridad" : "comparacion");
                }
                catch (Exception ex)
                {
                    Log.Advertencia("No se pudo guardar el reporte: " + ex.Message);
                }
                return r;
            }, r =>
            {
                ultimo = r;
                ultimoRespaldoId = id;
                Mostrar();
            });
        }

        private void Mostrar()
        {
            if (ultimo == null)
            {
                resumen.Ocultar();
                lblContadores.Text = "";
                grid.RowCount = 0;
                return;
            }
            visibles = ultimo.Items
                .Where(i => !chkSoloProblemas.Checked || i.Estado != EstadosVerificacion.Ok)
                .OrderBy(i => Orden(i.Estado)).ThenBy(i => i.Ruta, StringComparer.OrdinalIgnoreCase).ToList();
            grid.RowCount = 0;
            grid.RowCount = visibles.Count;
            grid.Invalidate();

            bool integridad = ultimo.Tipo.StartsWith("Integridad");
            if (ultimo.TodoBien)
            {
                string texto = integridad
                    ? "Respaldo sano: todo se puede restaurar exactamente igual. (" + ultimo.ObjetosRevisados + " contenidos comprobados)"
                    : "Los archivos de hoy son iguales al respaldo.";
                if (!integridad && ultimo.Cuenta(EstadosVerificacion.Nuevo) > 0) texto = "Nada se perdio ni cambio; hay archivos nuevos que no estan en el respaldo.";
                resumen.Mostrar(texto, Tema.BienFondo, Tema.Bien);
            }
            else if (integridad)
            {
                resumen.Mostrar("Hay contenido danado o faltante en el deposito. Vea el detalle abajo.", Tema.MalFondo, Tema.Mal);
            }
            else
            {
                resumen.Mostrar("Hay diferencias entre los archivos de hoy y el respaldo.", Tema.AvisoFondo, Tema.Aviso);
            }
            lblContadores.Text = ultimo.Tipo + "  |  " + ultimo.Resumen() + "  |  " + Tiempo.Legible(ultimo.Fin);
            if (ultimo.TodoBien && ultimo.Cuenta(EstadosVerificacion.Nuevo) > 0)
                resumen.Mostrar("Nada se perdio ni cambio. Hay " + ultimo.Cuenta(EstadosVerificacion.Nuevo) + " archivo(s) nuevo(s) desde el respaldo.", Tema.InfoFondo, Tema.Info);
            ActualizarBotones();
        }

        private static int Orden(string estado)
        {
            switch (estado)
            {
                case EstadosVerificacion.ManifiestoDanado: return 0;
                case EstadosVerificacion.Danado: return 1;
                case EstadosVerificacion.FaltaEnDeposito: return 2;
                case EstadosVerificacion.Ilegible: return 3;
                case EstadosVerificacion.Falta: return 4;
                case EstadosVerificacion.Modificado: return 5;
                case EstadosVerificacion.Nuevo: return 6;
                case EstadosVerificacion.Reparado: return 7;
                default: return 9;
            }
        }

        private void Valor(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= visibles.Count) return;
            ItemVerificacion i = visibles[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: e.Value = i.Estado; break;
                case 1: e.Value = i.Ruta; break;
                case 2: e.Value = i.Detalle; break;
                case 3: e.Value = i.Respaldo; break;
            }
        }

        private void Formato(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= visibles.Count) return;
            Color fondo, texto;
            if (Tema.ColoresDe(visibles[e.RowIndex].Estado, out fondo, out texto))
            {
                e.CellStyle.BackColor = fondo;
                e.CellStyle.ForeColor = texto;
            }
        }

        /// <summary>Pone de vuelta en el origen los archivos que faltan o cambiaron, desde el respaldo verificado.</summary>
        private void Reparar(bool soloSeleccionados)
        {
            if (ultimo == null || ultimoRespaldoId == null) return;
            IEnumerable<ItemVerificacion> candidatos = soloSeleccionados
                ? grid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Index >= 0 && r.Index < visibles.Count).Select(r => visibles[r.Index])
                : ultimo.Items;
            List<string> rutas = candidatos
                .Where(i => i.Estado == EstadosVerificacion.Falta || i.Estado == EstadosVerificacion.Modificado)
                .Select(i => i.Ruta).Distinct().ToList();
            if (rutas.Count == 0)
            {
                Aviso(soloSeleccionados ? "Seleccione archivos FALTA o MODIFICADO." : "No hay archivos que recuperar.");
                return;
            }
            if (!Sesion.Exigir(this, Permiso.Restaurar, "recuperar archivos en el origen")) return;
            if (!Confirmar("Se van a poner en su ubicacion original " + rutas.Count + " archivo(s) tal como estaban en el respaldo." +
                "\nLos MODIFICADOS se reemplazan por la version del respaldo." +
                "\n\nAntes se hace un respaldo automatico de como estan ahora, para poder volver atras.\nAsegurese de que el programa de la maquina este CERRADO. Continuar?", "Recuperar archivos"))
                return;
            string id = ultimoRespaldoId;
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Recuperando archivos", (p, c) => Servicio.Restaurar(id, null, rutas, true, usuario, p, c), r =>
            {
                var lineas = new List<string> { "Recuperados: " + r.Restaurados, "Ya estaban iguales: " + r.SinCambios };
                if (r.RespaldoPrevioId.Length > 0) lineas.Add("Respaldo automatico previo: " + r.RespaldoPrevioId);
                lineas.AddRange(r.Fallos.Select(f => "NO: " + f.Ruta + " -> " + f.Motivo));
                using (var d = new DialogoLista("Recuperar archivos", r.Fallos.Count == 0 ? "Archivos recuperados" : "Recuperacion con fallos", lineas, r.Fallos.Count == 0 ? Tema.Bien : Tema.Mal))
                    d.ShowDialog(this);
                ultimo = null;
                Mostrar();
            });
        }

        private void Exportar()
        {
            if (ultimo == null)
            {
                Aviso("Primero haga una verificacion.");
                return;
            }
            using (var d = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = "verificacion_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Seguro(() =>
                {
                    string generado = Verifier.GuardarReporte(ultimo, Path.GetDirectoryName(d.FileName), "tmp_export");
                    if (File.Exists(d.FileName)) File.Delete(d.FileName);
                    File.Move(generado, d.FileName);
                    Info("Reporte guardado en:\n" + d.FileName);
                });
            }
        }

        public static void AbrirCarpeta(string carpeta)
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                Process.Start("explorer.exe", "\"" + carpeta + "\"");
            }
            catch (Exception ex)
            {
                Log.Error("Abrir carpeta", ex);
            }
        }
    }
}
