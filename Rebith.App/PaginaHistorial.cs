using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    public class PaginaHistorial : PaginaBase
    {
        private class OpcionObjetivo
        {
            public string Id;
            public string Texto;
            public override string ToString() { return Texto; }
        }

        private readonly ComboBox cmbObjetivo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Tema.S(360) };
        private readonly DataGridView gridRespaldos = Tema.Tabla();
        private readonly DataGridView gridArchivos = Tema.Tabla();
        private readonly TextBox txtBuscar = new TextBox { Width = Tema.S(260) };
        private readonly Label lblArchivos = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave, Margin = new Padding(Tema.S(8), Tema.S(8), 0, 0) };
        private List<SnapshotManifest> respaldos = new List<SnapshotManifest>();
        private List<FileEntry> archivosVisibles = new List<FileEntry>();
        private SnapshotManifest seleccionado;
        private string objetivoPendiente;

        public PaginaHistorial(VentanaPrincipal v) : base(v)
        {
            gridRespaldos.Columns.AddRange(
                Tema.Columna("Fecha", "Fecha", 110), Tema.Columna("Estado", "Estado", 90), Tema.Columna("Archivos", "Archivos", 60),
                Tema.Columna("Tamano", "Tamano", 70), Tema.Columna("Omitidos", "Omitidos", 60), Tema.Columna("Usuario", "Usuario", 70),
                Tema.Columna("Nota", "Nota", 200), Tema.Columna("Id", "Id", 130));
            gridRespaldos.SelectionChanged += (s, e) => MostrarArchivos();

            gridArchivos.VirtualMode = true;
            gridArchivos.MultiSelect = true;
            gridArchivos.Columns.AddRange(Tema.Columna("Archivo", "Archivo", 300), Tema.Columna("Tamano", "Tamano", 60), Tema.Columna("Modificado", "Modificado", 80));
            foreach (DataGridViewColumn c in gridArchivos.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            gridArchivos.CellValueNeeded += ValorArchivo;
            txtBuscar.TextChanged += (s, e) => FiltrarArchivos();

            cmbObjetivo.SelectedIndexChanged += (s, e) => CargarRespaldos();

            var arriba = Tema.FilaBotones();
            arriba.Controls.Add(Tema.Etiqueta("Objetivo", Tema.Negrita));
            arriba.Controls.Add(cmbObjetivo);

            var botonesRespaldo = Tema.FilaBotones();
            botonesRespaldo.Dock = DockStyle.Bottom;
            botonesRespaldo.Controls.Add(Tema.Boton("Restaurar todo en otra carpeta...", (s, e) => Restaurar(false, false), true));
            botonesRespaldo.Controls.Add(Tema.Boton("Restaurar en su ubicacion original...", (s, e) => Restaurar(true, false)));
            botonesRespaldo.Controls.Add(Tema.Boton("Ver no respaldados", (s, e) => VerOmitidos()));
            botonesRespaldo.Controls.Add(Tema.Boton("Conservar ultimos...", (s, e) => ConservarUltimos()));
            botonesRespaldo.Controls.Add(Tema.BotonPeligro("Borrar respaldo", (s, e) => Borrar()));

            var panelRespaldos = new Panel { Dock = DockStyle.Fill };
            panelRespaldos.Controls.Add(gridRespaldos);
            panelRespaldos.Controls.Add(botonesRespaldo);

            var barraArchivos = Tema.FilaBotones();
            barraArchivos.Controls.Add(Tema.Etiqueta("Buscar archivo", Tema.Negrita));
            barraArchivos.Controls.Add(txtBuscar);
            barraArchivos.Controls.Add(Tema.Boton("Restaurar seleccionados...", (s, e) => Restaurar(false, true)));
            barraArchivos.Controls.Add(lblArchivos);
            var panelArchivos = new Panel { Dock = DockStyle.Fill };
            panelArchivos.Controls.Add(gridArchivos);
            panelArchivos.Controls.Add(barraArchivos);

            var division = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = Tema.S(8), BackColor = Tema.Fondo };
            division.Panel1.Controls.Add(panelRespaldos);
            division.Panel2.Controls.Add(panelArchivos);
            bool divisionLista = false;
            division.SizeChanged += (s, e) =>
            {
                if (divisionLista || division.Height < Tema.S(200)) return;
                divisionLista = true;
                try { division.SplitterDistance = division.Height * 45 / 100; } catch { }
            };

            Controls.Add(division);
            Controls.Add(arriba);
            Controls.Add(Tema.EncabezadoPagina("Historial y restaurar"));
        }

        public void Seleccionar(string targetId)
        {
            objetivoPendiente = targetId;
            AlRefrescar();
        }

        protected override void AlRefrescar()
        {
            Repository.LecturaManifiestos lectura = Servicio.Repo.LeerManifiestos();
            List<TargetInfo> objetivos = Servicio.Repo.LeerObjetivos();
            var opciones = objetivos.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new OpcionObjetivo { Id = t.Id, Texto = t.Name }).ToList();
            // Respaldos de objetivos que ya se quitaron: siguen visibles para poder restaurarlos.
            foreach (var g in lectura.Manifiestos.Where(m => objetivos.All(t => t.Id != m.TargetId)).GroupBy(m => m.TargetId))
                opciones.Add(new OpcionObjetivo { Id = g.Key, Texto = g.First().TargetName + " (objetivo quitado)" });

            string previo = objetivoPendiente ?? (cmbObjetivo.SelectedItem is OpcionObjetivo ? ((OpcionObjetivo)cmbObjetivo.SelectedItem).Id : null);
            objetivoPendiente = null;
            cmbObjetivo.BeginUpdate();
            cmbObjetivo.Items.Clear();
            foreach (OpcionObjetivo o in opciones) cmbObjetivo.Items.Add(o);
            cmbObjetivo.EndUpdate();
            int i = opciones.FindIndex(o => o.Id == previo);
            if (opciones.Count > 0) cmbObjetivo.SelectedIndex = i >= 0 ? i : 0;
            else CargarRespaldos();
        }

        private string ObjetivoActual()
        {
            var o = cmbObjetivo.SelectedItem as OpcionObjetivo;
            return o == null ? null : o.Id;
        }

        private void CargarRespaldos()
        {
            string id = ObjetivoActual();
            string previo = seleccionado == null ? null : seleccionado.Id;
            respaldos = id == null ? new List<SnapshotManifest>() : Servicio.Repo.RespaldosDe(id);
            gridRespaldos.Rows.Clear();
            foreach (SnapshotManifest m in respaldos)
            {
                int n = gridRespaldos.Rows.Add(Tiempo.Legible(m.StartedLocal), m.Status, m.Files.Count, Tamanos.Legible(m.TotalBytes),
                    m.Skipped.Count, m.User, m.Note, m.Id);
                gridRespaldos.Rows[n].Tag = m;
                Tema.ColorEstado(gridRespaldos.Rows[n], m.Status);
            }
            int i = respaldos.FindIndex(m => m.Id == previo);
            if (gridRespaldos.Rows.Count > 0)
            {
                gridRespaldos.ClearSelection();
                int fila = i >= 0 ? i : 0;
                gridRespaldos.Rows[fila].Selected = true;
                gridRespaldos.CurrentCell = gridRespaldos.Rows[fila].Cells[0];
            }
            MostrarArchivos();
        }

        private SnapshotManifest RespaldoSeleccionado()
        {
            if (gridRespaldos.SelectedRows.Count == 0) return null;
            return gridRespaldos.SelectedRows[0].Tag as SnapshotManifest;
        }

        private void MostrarArchivos()
        {
            seleccionado = RespaldoSeleccionado();
            FiltrarArchivos();
        }

        private void FiltrarArchivos()
        {
            string filtro = txtBuscar.Text.Trim();
            archivosVisibles = seleccionado == null ? new List<FileEntry>() :
                seleccionado.Files.Where(f => filtro.Length == 0 || f.Path.IndexOf(filtro, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            gridArchivos.RowCount = 0;
            gridArchivos.RowCount = archivosVisibles.Count;
            gridArchivos.Invalidate();
            lblArchivos.Text = seleccionado == null ? "" : archivosVisibles.Count + " de " + seleccionado.Files.Count + " archivos";
        }

        private void ValorArchivo(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= archivosVisibles.Count) return;
            FileEntry f = archivosVisibles[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: e.Value = f.Path; break;
                case 1: e.Value = Tamanos.Legible(f.Size); break;
                case 2: e.Value = new DateTime(f.MtimeUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm"); break;
            }
        }

        private void Restaurar(bool sobreOriginal, bool soloSeleccionados)
        {
            SnapshotManifest m = seleccionado;
            if (m == null)
            {
                Aviso("Elija un respaldo de la lista.");
                return;
            }
            List<string> rutas = null;
            if (soloSeleccionados)
            {
                rutas = gridArchivos.SelectedRows.Cast<DataGridViewRow>()
                    .Where(r => r.Index >= 0 && r.Index < archivosVisibles.Count)
                    .Select(r => archivosVisibles[r.Index].Path).Distinct().ToList();
                if (rutas.Count == 0)
                {
                    Aviso("Seleccione uno o mas archivos en la lista de abajo (Ctrl o Shift para varios).");
                    return;
                }
            }
            if (!Sesion.Exigir(this, Permiso.Restaurar, "restaurar")) return;

            string destino = null;
            if (sobreOriginal)
            {
                string original = m.IsFolder ? m.SourcePath : Path.GetDirectoryName(m.SourcePath);
                string texto = "Se van a REEMPLAZAR los archivos en su ubicacion original:\n\n" + original +
                    "\n\ncon los del respaldo del " + Tiempo.Legible(m.StartedLocal) + (rutas != null ? " (" + rutas.Count + " archivo(s))" : " (" + m.Files.Count + " archivos)") +
                    ".\n\nAntes se hara un respaldo automatico de como esta ahora, para poder volver atras." +
                    "\nLos archivos que no estan en el respaldo NO se borran." +
                    "\n\nAsegurese de que el programa de la maquina este CERRADO. Continuar?";
                if (!Confirmar(texto, "Restaurar en la ubicacion original")) return;
            }
            else
            {
                using (var d = new FolderBrowserDialog { Description = "Carpeta donde se van a poner los archivos restaurados" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    destino = d.SelectedPath;
                }
                if (!m.IsFolder || rutas != null)
                {
                    // un archivo o una seleccion: directo en la carpeta elegida
                }
                else
                {
                    destino = Path.Combine(destino, LimpiarNombre(m.TargetName) + "_" + m.StartedLocal.Replace(":", "").Replace("T", "_"));
                }
                if (Directory.Exists(destino) && Directory.EnumerateFileSystemEntries(destino).Any() &&
                    !Confirmar("La carpeta ya tiene archivos:\n" + destino + "\n\nLos que tengan el mismo nombre se reemplazan. Continuar?", "Carpeta con archivos"))
                    return;
            }

            string usuario = Sesion.ParaAuditoria;
            string destinoFinal = destino;
            List<string> seleccion = rutas;
            Ventana.Ejecutar("Restaurando " + m.TargetName, (p, c) => Servicio.Restaurar(m.Id, destinoFinal, seleccion, sobreOriginal, usuario, p, c), r =>
            {
                var lineas = new List<string>
                {
                    "Destino: " + r.Destino,
                    "Restaurados: " + r.Restaurados,
                    "Ya estaban iguales (no se tocaron): " + r.SinCambios,
                    "Cada archivo restaurado se comprobo con su huella SHA-256."
                };
                if (r.RespaldoPrevioId.Length > 0) lineas.Add("Respaldo automatico de lo que habia antes: " + r.RespaldoPrevioId);
                if (r.Fallos.Count > 0)
                {
                    lineas.Add("");
                    lineas.Add("NO SE PUDIERON RESTAURAR " + r.Fallos.Count + ":");
                    lineas.AddRange(r.Fallos.Select(f => "  " + f.Ruta + "  ->  " + f.Motivo));
                }
                bool bien = r.Fallos.Count == 0;
                using (var d = new DialogoLista("Resultado de la restauracion", bien ? "Restauracion correcta" : "Restauracion con fallos", lineas, bien ? Tema.Bien : Tema.Mal))
                    d.ShowDialog(this);
            });
        }

        private static string LimpiarNombre(string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Length == 0 ? "respaldo" : nombre;
        }

        private void VerOmitidos()
        {
            SnapshotManifest m = seleccionado;
            if (m == null) return;
            var lineas = m.Skipped.Count == 0
                ? new List<string> { "Todos los archivos se respaldaron." }
                : m.Skipped.Select(s => s.Path + "  ->  " + s.Reason).ToList();
            using (var d = new DialogoLista("No respaldados", m.Skipped.Count + " archivo(s) no se respaldaron en " + m.Id, lineas, m.Skipped.Count == 0 ? Tema.Bien : Tema.Aviso))
                d.ShowDialog(this);
        }

        private void Borrar()
        {
            SnapshotManifest m = seleccionado;
            if (m == null) return;
            if (!Sesion.Exigir(this, Permiso.Borrar, "borrar respaldos")) return;
            if (respaldos.Count == 1 && !Confirmar("Es el UNICO respaldo de este objetivo. Si lo borra no habra como restaurar. Continuar?", "Ultimo respaldo"))
                return;
            if (!Confirmar("Borrar el respaldo del " + Tiempo.Legible(m.StartedLocal) + " (" + m.Files.Count + " archivos)?\n\nEl contenido que usen otros respaldos se conserva.", "Borrar respaldo"))
                return;
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Borrando respaldo", (p, c) => Servicio.BorrarRespaldos(new[] { m.Id }, usuario, c), n => Info("Respaldo borrado."));
        }

        private void ConservarUltimos()
        {
            string id = ObjetivoActual();
            if (id == null) return;
            if (!Sesion.Exigir(this, Permiso.Borrar, "borrar respaldos viejos")) return;
            int cuantos;
            using (var d = new DialogoPedir("Conservar ultimos", "Se borran los respaldos mas viejos de este objetivo. Hay " + respaldos.Count + ".", "Conservar", 5, 1, 1000))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                cuantos = d.Numero;
            }
            int aBorrar = Math.Max(0, respaldos.Count - cuantos);
            if (aBorrar == 0)
            {
                Info("No hay nada que borrar.");
                return;
            }
            if (!Confirmar("Se borraran " + aBorrar + " respaldo(s) viejo(s). Continuar?", "Conservar ultimos")) return;
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Borrando respaldos viejos", (p, c) => Servicio.ConservarUltimos(id, cuantos, usuario, c), n => Info("Se borraron " + n + " respaldo(s)."));
        }
    }
}
