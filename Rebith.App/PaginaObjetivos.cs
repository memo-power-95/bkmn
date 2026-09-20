using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    /// <summary>Lista de objetivos (carpetas o archivos a respaldar) y su editor.</summary>
    public class PaginaObjetivos : PaginaBase
    {
        private readonly ListBox lista = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, Font = Tema.Normal };
        private readonly TextBox txtNombre = new TextBox();
        private readonly RadioButton rbCarpeta = new RadioButton { Text = "Carpeta", Checked = true, AutoSize = true };
        private readonly RadioButton rbArchivo = new RadioButton { Text = "Un archivo", AutoSize = true };
        private readonly TextBox txtRuta = new TextBox();
        private readonly TextBox txtExcluir = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = Tema.S(70) };
        private readonly ComboBox cmbFrecuencia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DateTimePicker hora = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = Tema.S(90) };
        private readonly ComboBox cmbDia = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Tema.S(130) };
        private readonly CheckBox chkInicio = new CheckBox { Text = "Tambien al abrir Rebith / al iniciar la PC", AutoSize = true };
        private readonly NumericUpDown numConservar = new NumericUpDown { Minimum = 0, Maximum = 1000, Width = Tema.S(80) };
        private readonly CheckBox chkLeerTodo = new CheckBox { Text = "Leer y comprobar TODOS los archivos en cada respaldo (mas lento, mas estricto)", AutoSize = true };
        private readonly Label lblEstado = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };
        private List<TargetInfo> objetivos = new List<TargetInfo>();
        private TargetInfo editando;
        private bool cargando;

        public PaginaObjetivos(VentanaPrincipal v) : base(v)
        {
            cmbFrecuencia.Items.AddRange(new object[] { Frecuencias.Manual, Frecuencias.Diario, Frecuencias.Semanal });
            cmbDia.Items.AddRange(new object[] { "Domingo", "Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado" });
            cmbFrecuencia.SelectedIndexChanged += (s, e) => ActualizarProgramacion();
            lista.SelectedIndexChanged += (s, e) => { if (!cargando) Cargar(lista.SelectedIndex >= 0 ? objetivos[lista.SelectedIndex] : null); };

            var izquierda = new Panel { Dock = DockStyle.Left, Width = Tema.S(260), Padding = new Padding(0, 0, Tema.S(16), 0) };
            var botonesLista = Tema.FilaBotones();
            botonesLista.Dock = DockStyle.Bottom;
            botonesLista.Controls.Add(Tema.Boton("+ Nuevo objetivo", (s, e) => Nuevo(), true));
            izquierda.Controls.Add(lista);
            izquierda.Controls.Add(botonesLista);

            var editor = new Tarjeta { Dock = DockStyle.Fill, AutoScroll = true };
            var campos = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Tema.S(170)));
            campos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Campo(campos, "Nombre", txtNombre);
            var tipo = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            tipo.Controls.Add(rbCarpeta);
            tipo.Controls.Add(rbArchivo);
            Campo(campos, "Que es", tipo);
            var filaRuta = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            filaRuta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filaRuta.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            txtRuta.Dock = DockStyle.Fill;
            filaRuta.Controls.Add(txtRuta, 0, 0);
            filaRuta.Controls.Add(Tema.Boton("Elegir...", (s, e) => ElegirRuta()), 1, 0);
            Campo(campos, "Ruta", filaRuta);
            Campo(campos, "No respaldar", txtExcluir);
            Campo(campos, "", Tema.Etiqueta("Un patron por linea. Ejemplos: *.log   *.tmp   Temp   cache/*   Log/*", Tema.Chica, Tema.TextoSuave));
            var prog = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            prog.Controls.Add(cmbFrecuencia);
            prog.Controls.Add(Tema.Etiqueta("a las"));
            prog.Controls.Add(hora);
            prog.Controls.Add(cmbDia);
            Campo(campos, "Respaldo automatico", prog);
            Campo(campos, "", chkInicio);
            var conservar = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            conservar.Controls.Add(numConservar);
            conservar.Controls.Add(Tema.Etiqueta("respaldos mas recientes (0 = conservar todos)", Tema.Chica, Tema.TextoSuave));
            Campo(campos, "Conservar", conservar);
            Campo(campos, "", chkLeerTodo);

            var acciones = Tema.FilaBotones();
            acciones.Controls.Add(Tema.Boton("Guardar", (s, e) => Guardar(), true));
            acciones.Controls.Add(Tema.Boton("Respaldar ahora", (s, e) => { if (Guardar()) RespaldarAhora(editando, true); }));
            acciones.Controls.Add(Tema.Boton("Probar que se incluye", (s, e) => Probar()));
            acciones.Controls.Add(Tema.BotonPeligro("Quitar objetivo", (s, e) => Quitar()));
            var pie = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            pie.Controls.Add(lblEstado);

            editor.Controls.Add(pie);
            editor.Controls.Add(acciones);
            editor.Controls.Add(campos);

            var cuerpo = new Panel { Dock = DockStyle.Fill };
            cuerpo.Controls.Add(editor);
            cuerpo.Controls.Add(izquierda);
            Controls.Add(cuerpo);
            Controls.Add(Tema.Ayuda("Cada objetivo es una carpeta (por ejemplo la de una maquina) o un archivo. Los respaldos solo guardan lo que cambio, asi que respaldar seguido no llena el disco."));
            Controls.Add(Tema.EncabezadoPagina("Que respaldar"));
            Cargar(null);
        }

        private static void Campo(TableLayoutPanel t, string etiqueta, Control control)
        {
            Label l = Tema.Etiqueta(etiqueta, Tema.Negrita);
            l.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            l.Margin = new Padding(0, Tema.S(8), Tema.S(8), 0);
            if (!(control is FlowLayoutPanel) && !(control is TableLayoutPanel) && !(control is CheckBox) && !(control is Label))
                control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            control.Margin = new Padding(0, Tema.S(4), 0, Tema.S(4));
            int fila = t.RowCount;
            t.Controls.Add(l, 0, fila);
            t.Controls.Add(control, 1, fila);
            t.RowCount++;
        }

        protected override void AlRefrescar()
        {
            string seleccionado = editando == null ? null : editando.Id;
            objetivos = Servicio.Repo.LeerObjetivos().OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
            cargando = true;
            lista.Items.Clear();
            foreach (TargetInfo t in objetivos) lista.Items.Add(t.Name);
            int i = objetivos.FindIndex(t => t.Id == seleccionado);
            if (i >= 0) lista.SelectedIndex = i;
            cargando = false;
            if (i >= 0) Cargar(objetivos[i]);
            else if (editando != null && !string.IsNullOrEmpty(editando.Id)) Cargar(null);
        }

        public void Seleccionar(string id)
        {
            int i = objetivos.FindIndex(t => t.Id == id);
            if (i >= 0) lista.SelectedIndex = i;
        }

        private void Nuevo()
        {
            lista.ClearSelected();
            Cargar(null);
            txtNombre.Focus();
        }

        private void Cargar(TargetInfo t)
        {
            editando = t ?? new TargetInfo();
            TargetInfo e = editando;
            txtNombre.Text = e.Name;
            txtRuta.Text = e.SourcePath;
            rbCarpeta.Checked = e.IsFolder;
            rbArchivo.Checked = !e.IsFolder;
            txtExcluir.Text = string.Join(Environment.NewLine, e.Excludes);
            cmbFrecuencia.SelectedItem = e.Schedule.Frequency;
            if (cmbFrecuencia.SelectedIndex < 0) cmbFrecuencia.SelectedIndex = 0;
            hora.Value = DateTime.Today.AddHours(e.Schedule.Hour).AddMinutes(e.Schedule.Minute);
            cmbDia.SelectedIndex = Math.Max(0, Math.Min(6, e.Schedule.DayOfWeek));
            chkInicio.Checked = e.Schedule.RunOnStartup;
            numConservar.Value = Math.Max(0, Math.Min(1000, e.KeepLast));
            chkLeerTodo.Checked = e.AlwaysReadAll;
            lblEstado.Text = string.IsNullOrEmpty(e.Id) ? "Objetivo nuevo (sin guardar)." :
                "Creado " + Tiempo.Legible(e.CreatedLocal) + ". " + Programador.Describir(e) +
                (e.Schedule.LastRunLocal.Length > 0 ? ". Ultimo programado: " + Tiempo.Legible(e.Schedule.LastRunLocal) + " " + e.Schedule.LastResult : "");
            ActualizarProgramacion();
        }

        private void ActualizarProgramacion()
        {
            string f = cmbFrecuencia.SelectedItem as string ?? Frecuencias.Manual;
            hora.Enabled = f != Frecuencias.Manual;
            cmbDia.Visible = f == Frecuencias.Semanal;
            chkInicio.Enabled = f != Frecuencias.Manual;
        }

        private void ElegirRuta()
        {
            if (rbArchivo.Checked)
            {
                using (var d = new OpenFileDialog { Title = "Archivo a respaldar", CheckFileExists = true })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    txtRuta.Text = d.FileName;
                }
            }
            else
            {
                using (var d = new FolderBrowserDialog { Description = "Carpeta a respaldar", SelectedPath = Directory.Exists(txtRuta.Text) ? txtRuta.Text : "" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    txtRuta.Text = d.SelectedPath;
                }
            }
            if (txtNombre.Text.Trim().Length == 0)
                txtNombre.Text = Path.GetFileName(txtRuta.Text.TrimEnd('\\', '/'));
        }

        private TargetInfo LeerFormulario()
        {
            var t = new TargetInfo
            {
                Id = editando.Id,
                CreatedLocal = editando.CreatedLocal,
                Name = txtNombre.Text,
                SourcePath = txtRuta.Text,
                IsFolder = rbCarpeta.Checked,
                Excludes = Exclusiones.DesdeTexto(txtExcluir.Text),
                KeepLast = (int)numConservar.Value,
                AlwaysReadAll = chkLeerTodo.Checked,
                Schedule = new ScheduleInfo
                {
                    Frequency = cmbFrecuencia.SelectedItem as string ?? Frecuencias.Manual,
                    Hour = hora.Value.Hour,
                    Minute = hora.Value.Minute,
                    DayOfWeek = Math.Max(0, cmbDia.SelectedIndex),
                    RunOnStartup = chkInicio.Checked,
                    LastRunLocal = editando.Schedule.LastRunLocal,
                    LastResult = editando.Schedule.LastResult,
                    RetryAfterLocal = editando.Schedule.RetryAfterLocal
                }
            };
            return t;
        }

        private bool Guardar()
        {
            if (!Sesion.Exigir(this, Permiso.Respaldar, "configurar objetivos")) return false;
            TargetInfo t = LeerFormulario();
            bool existe = t.IsFolder ? Directory.Exists(t.SourcePath) : File.Exists(t.SourcePath);
            if (!existe && !Confirmar("La ruta no existe ahora mismo:\n" + t.SourcePath + "\n\nGuardar de todos modos? (por ejemplo, si es un disco de red que se conecta despues)", "Ruta no encontrada"))
                return false;
            if (t.KeepLast > 0 && t.KeepLast < 3 && !Confirmar("Conservar solo " + t.KeepLast + " respaldo(s) deja poco margen para volver atras. Continuar?", "Conservar pocos respaldos"))
                return false;
            try
            {
                bool nuevo = string.IsNullOrEmpty(t.Id);
                t = Servicio.Repo.GuardarObjetivo(t);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, nuevo ? "objetivo_creado" : "objetivo_editado",
                    string.Format("{0} -> {1} | {2} | excluir: {3} | conservar: {4}", t.Name, t.SourcePath, Programador.Describir(t), string.Join(" ", t.Excludes), t.KeepLast));
                editando = t;
                AlRefrescar();
                lblEstado.Text = "Guardado. " + Programador.Describir(t) + ".";
                return true;
            }
            catch (RebithException ex)
            {
                Aviso(ex.Message);
                return false;
            }
        }

        private void Quitar()
        {
            if (string.IsNullOrEmpty(editando.Id)) return;
            if (!Sesion.Exigir(this, Permiso.Borrar, "quitar objetivos")) return;
            if (!Confirmar("Quitar el objetivo \"" + editando.Name + "\"?\n\nSus respaldos NO se borran: siguen en el historial y se pueden restaurar.", "Quitar objetivo")) return;
            Seguro(() =>
            {
                Servicio.Repo.QuitarObjetivo(editando.Id);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, "objetivo_quitado", editando.Name + " (" + editando.SourcePath + ")");
                editando = null;
                Cargar(null);
                AlRefrescar();
            });
        }

        /// <summary>Cuenta que archivos entran y cuales se excluyen, sin respaldar nada.</summary>
        private void Probar()
        {
            TargetInfo t = LeerFormulario();
            if (!t.IsFolder || !Directory.Exists(t.SourcePath))
            {
                Aviso("Elija primero una carpeta que exista.");
                return;
            }
            Ventana.Ejecutar("Revisando la carpeta", (p, c) =>
            {
                var omitidos = new List<SkippedEntry>();
                List<ArchivoEncontrado> incluidos = Recorrido.Archivos(t.SourcePath, new Exclusiones(t.Excludes), omitidos, () => c.IsCancellationRequested);
                List<ArchivoEncontrado> todos = Recorrido.Archivos(t.SourcePath, null, null, () => c.IsCancellationRequested);
                long bytes = 0;
                foreach (ArchivoEncontrado a in incluidos)
                {
                    try { bytes += new FileInfo(a.RutaCompleta).Length; } catch { }
                }
                var incl = new HashSet<string>(incluidos.Select(a => a.RutaRelativa));
                var lineas = new List<string> { "Se respaldarian " + incluidos.Count + " archivos (" + Tamanos.Legible(bytes) + ").", "Excluidos por los patrones: " + (todos.Count - incluidos.Count) + "." };
                lineas.AddRange(omitidos.Select(o => "No se puede leer: " + o.Path + " -> " + o.Reason));
                lineas.Add("");
                lineas.Add("EXCLUIDOS (primeros 300):");
                lineas.AddRange(todos.Where(a => !incl.Contains(a.RutaRelativa)).Take(300).Select(a => "  " + a.RutaRelativa));
                return lineas;
            }, lineas =>
            {
                using (var d = new DialogoLista("Que se incluye", t.Name, lineas, Tema.Texto)) d.ShowDialog(this);
            });
        }
    }
}
