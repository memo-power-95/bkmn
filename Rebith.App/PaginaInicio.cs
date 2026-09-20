using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Rebith.Core;

namespace Rebith.App
{
    /// <summary>Base de todas las paginas: acceso al servicio y manejo seguro de errores.</summary>
    public class PaginaBase : UserControl, IPagina
    {
        protected readonly VentanaPrincipal Ventana;
        protected RebithService Servicio { get { return Ventana.Servicio; } }
        protected Sesion Sesion { get { return Ventana.Sesion; } }

        protected PaginaBase(VentanaPrincipal ventana)
        {
            Ventana = ventana;
            BackColor = Tema.Fondo;
            Font = Tema.Normal;
            AutoScaleMode = AutoScaleMode.None;
            Tema.DobleBuffer(this);
        }

        public void Refrescar()
        {
            try
            {
                AlRefrescar();
            }
            catch (Exception ex)
            {
                Log.Error(GetType().Name + ".Refrescar", ex);
                Aviso("No se pudo leer la informacion: " + ex.Message);
            }
        }

        protected virtual void AlRefrescar() { }

        /// <summary>Corre una accion rapida de la interfaz; si falla, avisa sin cerrar nada.</summary>
        protected void Seguro(Action accion)
        {
            try
            {
                accion();
            }
            catch (RebithException ex)
            {
                Aviso(ex.Message);
            }
            catch (Exception ex)
            {
                Program.ErrorInesperado(ex);
            }
        }

        protected void Aviso(string texto)
        {
            MessageBox.Show(this, texto, "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected void Info(string texto)
        {
            MessageBox.Show(this, texto, "Rebith", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected bool Confirmar(string texto, string titulo)
        {
            return MessageBox.Show(this, texto, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        /// <summary>Pide nota y hace el respaldo del objetivo, con resultado claro al final.</summary>
        protected void RespaldarAhora(TargetInfo t, bool pedirNota)
        {
            if (!Sesion.Exigir(this, Permiso.Respaldar, "respaldar")) return;
            string nota = "";
            if (pedirNota)
            {
                using (var d = new DialogoPedir("Respaldar " + t.Name, "Nota para este respaldo (opcional). Ejemplo: antes de cambiar la receta.", "Nota", ""))
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    nota = d.Texto;
                }
            }
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Respaldo de " + t.Name, (p, c) => Servicio.Respaldar(t.Id, nota, usuario, p, c), r => MostrarResultadoRespaldo(r));
        }

        protected void MostrarResultadoRespaldo(RebithService.ResultadoOperacion r)
        {
            SnapshotManifest m = r.Respaldo.Manifiesto;
            var lineas = new List<string>
            {
                "Archivos: " + m.Files.Count + "  (" + Tamanos.Legible(m.TotalBytes) + ")",
                "Contenido nuevo guardado: " + m.NewObjects + "  (" + Tamanos.Legible(m.NewBytesStored) + " comprimido)",
                "Sin cambios desde el respaldo anterior (no se volvieron a leer): " + r.Respaldo.Reutilizados,
                "Duracion: " + r.Respaldo.Duracion.TotalSeconds.ToString("0.0") + " s",
                "Todo lo nuevo se releyo y se comprobo su huella: correcto."
            };
            if (r.Respaldo.Reparados > 0) lineas.Add("Se repararon " + r.Respaldo.Reparados + " contenido(s) que estaban danados en el deposito.");
            foreach (string a in r.Avisos) lineas.Add("Aviso: " + a);
            if (m.Skipped.Count > 0)
            {
                lineas.Add("");
                lineas.Add("NO SE RESPALDARON " + m.Skipped.Count + " archivo(s):");
                lineas.AddRange(m.Skipped.Select(s => "  " + s.Path + "  ->  " + s.Reason));
            }
            bool bien = m.Skipped.Count == 0 && r.Avisos.Count == 0;
            using (var d = new DialogoLista("Resultado del respaldo",
                (bien ? "Respaldo correcto" : "Respaldo con advertencias") + " - " + m.TargetName,
                lineas, bien ? Tema.Bien : Tema.Aviso))
            {
                d.ShowDialog(this);
            }
        }
    }

    public class PaginaInicio : PaginaBase
    {
        private readonly Banner avisos = new Banner();
        private readonly FlowLayoutPanel resumen = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        private readonly FlowLayoutPanel tarjetas = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(0, Tema.S(8), 0, 0) };
        private readonly Label lblAvisosDetalle = new Label { Dock = DockStyle.Top, AutoSize = true, Font = Tema.Chica, ForeColor = Tema.Aviso, Padding = new Padding(0, Tema.S(4), 0, Tema.S(6)) };
        private Label lblEspacio;

        public PaginaInicio(VentanaPrincipal v) : base(v)
        {
            var titulo = new Label { Text = "Estado de los respaldos", Dock = DockStyle.Top, Height = Tema.S(34), Font = Tema.Subtitulo, ForeColor = Tema.Texto };
            Controls.Add(tarjetas);
            Controls.Add(titulo);
            Controls.Add(lblAvisosDetalle);
            Controls.Add(resumen);
            Controls.Add(avisos);
            Controls.Add(Tema.EncabezadoPagina("Inicio"));
        }

        protected override void AlRefrescar()
        {
            Repository.LecturaManifiestos lectura = Servicio.Repo.LeerManifiestos();
            List<TargetInfo> objetivos = Servicio.Repo.LeerObjetivos();
            RepoSettings ajustes = Servicio.Repo.LeerAjustes();

            // ---- resumen
            resumen.Controls.Clear();
            SnapshotManifest ultimo = lectura.Manifiestos.FirstOrDefault();
            resumen.Controls.Add(Dato("Objetivos", objetivos.Count.ToString()));
            resumen.Controls.Add(Dato("Respaldos guardados", lectura.Manifiestos.Count.ToString()));
            resumen.Controls.Add(Dato("Ultimo respaldo", ultimo == null ? "Nunca" : Tiempo.Legible(ultimo.FinishedLocal)));
            Tarjeta espacio = Dato("Espacio usado", "calculando...");
            lblEspacio = espacio.Controls.OfType<Label>().Last();
            resumen.Controls.Add(espacio);
            CalcularEspacio();

            // ---- avisos
            var problemas = new List<string>();
            if (lectura.Danados.Count > 0) problemas.Add(lectura.Danados.Count + " respaldo(s) con la lista de archivos danada (ver Verificar).");
            int danados = Servicio.Repo.LeerDanados().Count;
            if (danados > 0) problemas.Add(danados + " contenido(s) danados en el deposito; se reparan en el siguiente respaldo o desde la replica.");
            if (!ajustes.LastAutoVerifyOk) problemas.Add("La ultima revision automatica encontro problemas: " + ajustes.LastAutoVerifyResult);
            long libre = Rutas.EspacioLibre(Servicio.Repo.Raiz);
            if (libre >= 0 && libre < (long)ajustes.MinFreeSpaceMB * 1024 * 1024 * 2)
                problemas.Add("Queda poco espacio en el disco del deposito: " + Tamanos.Legible(libre) + ".");
            foreach (TargetInfo t in objetivos)
            {
                if (Rutas.MismaUnidad(t.SourcePath, Servicio.Repo.Raiz))
                {
                    problemas.Add("\"" + t.Name + "\" y el deposito estan en el mismo disco: si el disco falla se pierden ambos. Use una replica en otro disco.");
                    break;
                }
            }
            string problemaReplica;
            Servicio.AbrirReplica(out problemaReplica);
            if (problemaReplica != null) problemas.Add(problemaReplica);
            foreach (TargetInfo t in objetivos.Where(x => x.Schedule.LastResult.StartsWith("ERROR")))
                problemas.Add("Respaldo programado de \"" + t.Name + "\": " + t.Schedule.LastResult);

            if (problemas.Count == 0)
            {
                avisos.Mostrar("Todo en orden.", Tema.BienFondo, Tema.Bien);
                lblAvisosDetalle.Text = "";
            }
            else
            {
                bool grave = lectura.Danados.Count > 0 || danados > 0 || !ajustes.LastAutoVerifyOk;
                avisos.Mostrar(problemas.Count == 1 ? "Hay 1 punto que revisar" : "Hay " + problemas.Count + " puntos que revisar",
                    grave ? Tema.MalFondo : Tema.AvisoFondo, grave ? Tema.Mal : Tema.Aviso);
                lblAvisosDetalle.Text = string.Join(Environment.NewLine, problemas.Select(x => "- " + x));
            }

            // ---- una tarjeta por objetivo
            tarjetas.SuspendLayout();
            tarjetas.Controls.Clear();
            if (objetivos.Count == 0)
            {
                var vacio = new Tarjeta { Width = Tema.S(520), Height = Tema.S(120) };
                vacio.Controls.Add(Tema.Etiqueta("Todavia no hay nada que respaldar.\nAgregue la carpeta de una maquina en \"Que respaldar\".", Tema.Normal, Tema.TextoSuave));
                Button ir = Tema.Boton("Agregar objetivo", (s, e) => Ventana.Navegar(Paginas.Objetivos), true);
                ir.Location = new Point(Tema.S(14), Tema.S(64));
                vacio.Controls.Add(ir);
                tarjetas.Controls.Add(vacio);
            }
            foreach (TargetInfo t in objetivos.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                tarjetas.Controls.Add(TarjetaObjetivo(t, lectura.Manifiestos.Where(m => m.TargetId == t.Id).ToList()));
            tarjetas.ResumeLayout();
        }

        private void CalcularEspacio()
        {
            Label destino = lblEspacio;
            Repository repo = Servicio.Repo;
            Task.Run(() => repo.TamanoObjetos()).ContinueWith(t =>
            {
                if (destino.IsDisposed) return;
                destino.Text = t.IsFaulted ? "-" : Tamanos.Legible(t.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private static Tarjeta Dato(string titulo, string valor)
        {
            var c = new Tarjeta { Width = Tema.S(200), Height = Tema.S(82) };
            var t = new Label { Text = titulo, Location = new Point(Tema.S(14), Tema.S(10)), AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };
            var v = new Label { Text = valor, Location = new Point(Tema.S(14), Tema.S(32)), AutoSize = true, Font = Tema.Subtitulo, ForeColor = Tema.Texto };
            c.Controls.Add(t);
            c.Controls.Add(v);
            return c;
        }

        private Control TarjetaObjetivo(TargetInfo t, List<SnapshotManifest> respaldos)
        {
            var c = new Tarjeta { Width = Tema.S(430), Height = Tema.S(200) };
            SnapshotManifest ultimo = respaldos.FirstOrDefault();
            Color color = ultimo == null ? Tema.TextoSuave : (ultimo.Status == EstadosSnapshot.Completo ? Tema.Bien : Tema.Aviso);
            string estado = ultimo == null ? "Sin respaldos" : ultimo.Status + " - " + Tiempo.Legible(ultimo.FinishedLocal);
            DateTime? proxima = Programador.ProximaEjecucion(t, DateTime.Now);
            bool existe = System.IO.Directory.Exists(t.SourcePath) || System.IO.File.Exists(t.SourcePath);

            var nombre = new Label { Text = t.Name, Location = new Point(Tema.S(14), Tema.S(10)), AutoSize = true, Font = Tema.Subtitulo, ForeColor = Tema.Texto };
            var ruta = new Label { Text = t.SourcePath, Location = new Point(Tema.S(14), Tema.S(38)), Size = new Size(Tema.S(400), Tema.S(20)), AutoEllipsis = true, Font = Tema.Chica, ForeColor = existe ? Tema.TextoSuave : Tema.Mal };
            var lblEstado = new Label { Text = "Ultimo: " + estado, Location = new Point(Tema.S(14), Tema.S(64)), AutoSize = true, Font = Tema.Negrita, ForeColor = color };
            var lblCuenta = new Label { Text = respaldos.Count + " respaldo(s) guardado(s)" + (t.KeepLast > 0 ? ", se conservan " + t.KeepLast : ""), Location = new Point(Tema.S(14), Tema.S(88)), AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };
            string textoProg = Programador.Describir(t) + (proxima.HasValue ? " (proximo: " + proxima.Value.ToString("dd/MM HH:mm") + ")" : "");
            var lblProg = new Label { Text = textoProg, Location = new Point(Tema.S(14), Tema.S(108)), AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };
            if (!existe) lblCuenta.Text += "  |  LA RUTA NO EXISTE";

            Button respaldar = Tema.Boton("Respaldar ahora", (s, e) => RespaldarAhora(t, true), true);
            Button historial = Tema.Boton("Historial", (s, e) => Ventana.Pagina<PaginaHistorial>(Paginas.Historial).Seleccionar(t.Id));
            respaldar.Location = new Point(Tema.S(14), Tema.S(142));
            historial.Location = new Point(Tema.S(14) + respaldar.PreferredSize.Width + Tema.S(8), Tema.S(142));
            c.Controls.AddRange(new Control[] { nombre, ruta, lblEstado, lblCuenta, lblProg, respaldar, historial });
            return c;
        }
    }
}
