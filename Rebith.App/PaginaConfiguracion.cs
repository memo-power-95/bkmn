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
    public class PaginaConfiguracion : PaginaBase
    {
        private const string NombreTarea = "Rebith\\Respaldos programados";

        private readonly Label lblDeposito = new Label { AutoSize = true, Font = Tema.Negrita };
        private readonly Label lblEspacio = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };
        private readonly TextBox txtReplica = new TextBox { Width = Tema.S(420) };
        private readonly Label lblReplica = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };
        private readonly NumericUpDown numDias = new NumericUpDown { Minimum = 0, Maximum = 90, Width = Tema.S(70) };
        private readonly Label lblRevision = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave, MaximumSize = new Size(Tema.S(760), 0) };
        private readonly NumericUpDown numEspacio = new NumericUpDown { Minimum = 0, Maximum = 1000000, Increment = 512, Width = Tema.S(100) };
        private readonly NumericUpDown numSesion = new NumericUpDown { Minimum = 1, Maximum = 240, Width = Tema.S(70) };
        private readonly Label lblTarea = new Label { AutoSize = true, Font = Tema.Chica, ForeColor = Tema.TextoSuave };

        public PaginaConfiguracion(VentanaPrincipal v) : base(v)
        {
            var columna = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

            Tarjeta deposito = Seccion("Deposito de respaldos", "La carpeta donde se guarda todo. Lo ideal es un disco distinto al de las maquinas o una carpeta de red.");
            Agregar(deposito, lblDeposito);
            Agregar(deposito, lblEspacio);
            Agregar(deposito, Fila(
                Tema.Boton("Abrir carpeta", (s, e) => PaginaVerificar.AbrirCarpeta(Servicio.Repo.Raiz)),
                Tema.Boton("Cambiar de deposito...", (s, e) => CambiarDeposito()),
                Tema.Boton("Limpiar contenido sin uso", (s, e) => Limpiar())));
            columna.Controls.Add(deposito);

            Tarjeta replica = Seccion("Replica (segunda copia)", "Cada respaldo se copia tambien aqui. Si algo se dana en el deposito, la verificacion lo repara desde la replica. Use otro disco o la red.");
            Agregar(replica, Fila(txtReplica, Tema.Boton("Elegir...", (s, e) => ElegirReplica())));
            Agregar(replica, lblReplica);
            Agregar(replica, Fila(Tema.Boton("Guardar replica", (s, e) => GuardarReplica(), true), Tema.Boton("Sincronizar ahora", (s, e) => Sincronizar())));
            columna.Controls.Add(replica);

            Tarjeta revision = Seccion("Revision automatica", "Revisa sola que TODO el deposito se pueda restaurar (descomprime y compara huellas). Solo vuelve a leer lo que no se reviso en ese periodo.");
            Agregar(revision, Fila(Tema.Etiqueta("Cada"), numDias, Tema.Etiqueta("dias (0 = nunca)")));
            Agregar(revision, lblRevision);
            columna.Controls.Add(revision);

            Tarjeta seguridad = Seccion("Seguridad y espacio", null);
            Agregar(seguridad, Fila(Tema.Etiqueta("No respaldar si quedan menos de"), numEspacio, Tema.Etiqueta("MB libres")));
            Agregar(seguridad, Fila(Tema.Etiqueta("Cerrar la sesion tras"), numSesion, Tema.Etiqueta("minutos sin uso")));
            Agregar(seguridad, Fila(Tema.Boton("Guardar", (s, e) => GuardarAjustes(), true)));
            columna.Controls.Add(seguridad);

            Tarjeta tarea = Seccion("Respaldos con la ventana cerrada", "Registra a Rebith en el Programador de tareas de Windows (cada 15 minutos revisa si toca algo). Asi los respaldos programados corren aunque nadie abra la ventana.");
            Agregar(tarea, lblTarea);
            Agregar(tarea, Fila(Tema.Boton("Registrar", (s, e) => RegistrarTarea(), true), Tema.Boton("Quitar", (s, e) => QuitarTarea()), Tema.Boton("Abrir logs", (s, e) => PaginaVerificar.AbrirCarpeta(Log.Carpeta ?? RebithService.CarpetaLocal))));
            columna.Controls.Add(tarea);

            Tarjeta importar = Seccion("Version anterior (Python)", "Trae los respaldos, usuarios y auditoria de la version anterior (backups.db y su carpeta storage). No modifica nada de la version anterior. Se puede repetir sin duplicar.");
            Agregar(importar, Fila(Tema.Boton("Importar...", (s, e) => Importar())));
            columna.Controls.Add(importar);

            Controls.Add(columna);
            Controls.Add(Tema.EncabezadoPagina("Configuracion"));
        }

        private static Tarjeta Seccion(string titulo, string ayuda)
        {
            var t = new Tarjeta { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(Tema.S(800), 0) };
            var interior = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(Tema.S(14), Tema.S(14)),
                Name = "interior"
            };
            interior.Controls.Add(Tema.Etiqueta(titulo, Tema.Subtitulo));
            if (ayuda != null)
            {
                Label l = Tema.Etiqueta(ayuda, Tema.Chica, Tema.TextoSuave);
                l.MaximumSize = new Size(Tema.S(760), 0);
                interior.Controls.Add(l);
            }
            t.Controls.Add(interior);
            return t;
        }

        private static void Agregar(Tarjeta t, Control c)
        {
            t.Controls["interior"].Controls.Add(c);
        }

        private static FlowLayoutPanel Fila(params Control[] controles)
        {
            var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, Tema.S(4), 0, 0) };
            foreach (Control c in controles)
            {
                if (c is Label) c.Margin = new Padding(0, Tema.S(8), Tema.S(6), 0);
                f.Controls.Add(c);
            }
            return f;
        }

        protected override void AlRefrescar()
        {
            RepoSettings a = Servicio.Repo.LeerAjustes();
            lblDeposito.Text = Servicio.Repo.Raiz;
            long libre = Rutas.EspacioLibre(Servicio.Repo.Raiz);
            lblEspacio.Text = libre >= 0 ? "Espacio libre en ese disco: " + Tamanos.Legible(libre) : "Carpeta de red (espacio libre no disponible).";
            txtReplica.Text = a.ReplicaPath;
            string problema;
            Repository r = Servicio.AbrirReplica(out problema);
            lblReplica.Text = string.IsNullOrEmpty(a.ReplicaPath) ? "Sin replica: si el disco del deposito falla, se pierden los respaldos."
                : problema ?? ("Disponible. " + r.LeerManifiestos().Manifiestos.Count + " respaldos en la replica.");
            lblReplica.ForeColor = string.IsNullOrEmpty(a.ReplicaPath) || problema != null ? Tema.Aviso : Tema.Bien;
            numDias.Value = Math.Max(0, Math.Min(90, a.AutoVerifyDays));
            lblRevision.Text = a.LastAutoVerifyLocal.Length == 0 ? "Todavia no ha corrido." :
                "Ultima: " + Tiempo.Legible(a.LastAutoVerifyLocal) + (a.LastAutoVerifyOk ? " - todo bien. " : " - CON PROBLEMAS. ") + a.LastAutoVerifyResult;
            lblRevision.ForeColor = a.LastAutoVerifyOk ? Tema.TextoSuave : Tema.Mal;
            numEspacio.Value = Math.Max(0, Math.Min(1000000, a.MinFreeSpaceMB));
            numSesion.Value = Math.Max(1, Math.Min(240, a.SessionTimeoutMinutes));
            lblTarea.Text = EstadoTarea();
        }

        private void GuardarAjustes()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "cambiar la configuracion")) return;
            Seguro(() =>
            {
                RepoSettings a = Servicio.Repo.LeerAjustes();
                a.AutoVerifyDays = (int)numDias.Value;
                a.MinFreeSpaceMB = (int)numEspacio.Value;
                a.SessionTimeoutMinutes = (int)numSesion.Value;
                Servicio.Repo.GuardarAjustes(a);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, "ajustes", string.Format("revision cada {0} dias, minimo {1} MB, sesion {2} min", a.AutoVerifyDays, a.MinFreeSpaceMB, a.SessionTimeoutMinutes));
                Info("Configuracion guardada.");
                AlRefrescar();
            });
        }

        private void ElegirReplica()
        {
            using (var d = new FolderBrowserDialog { Description = "Carpeta para la replica (vacia, en OTRO disco o en la red)" })
            {
                if (d.ShowDialog(this) == DialogResult.OK) txtReplica.Text = d.SelectedPath;
            }
        }

        private void GuardarReplica()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "configurar la replica")) return;
            string ruta = txtReplica.Text.Trim();
            Seguro(() =>
            {
                if (ruta.Length > 0)
                {
                    if (Rutas.SeTraslapan(ruta, Servicio.Repo.Raiz)) throw new RebithException("La replica no puede estar dentro del deposito ni contenerlo.");
                    if (Rutas.MismaUnidad(ruta, Servicio.Repo.Raiz) &&
                        !Confirmar("La replica esta en el MISMO disco que el deposito: no protege si ese disco falla. Guardar de todos modos?", "Mismo disco"))
                        return;
                    Repository.AbrirOCrear(ruta);
                }
                RepoSettings a = Servicio.Repo.LeerAjustes();
                a.ReplicaPath = ruta;
                Servicio.Repo.GuardarAjustes(a);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, "replica", ruta.Length == 0 ? "sin replica" : ruta);
                AlRefrescar();
                if (ruta.Length > 0 && Confirmar("Replica guardada. Copiar ahora todos los respaldos existentes a la replica?", "Replica"))
                    Sincronizar();
            });
        }

        private void Sincronizar()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "sincronizar la replica")) return;
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Sincronizando replica", (p, c) => Servicio.SincronizarReplica(usuario, p, c), r =>
                Info(string.Format("Replica sincronizada.\n\nContenidos copiados: {0}\nRespaldos copiados: {1}\nRespaldos quitados de la replica: {2}\nFallos: {3}",
                    r.ObjetosCopiados, r.RespaldosCopiados, r.RespaldosQuitados, r.Fallos)));
        }

        private void Limpiar()
        {
            if (!Sesion.Exigir(this, Permiso.Borrar, "limpiar el deposito")) return;
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Limpiando contenido sin uso", (p, c) => Servicio.Limpiar(usuario, c), r =>
                Info(r.Cancelada ? r.Motivo : "Se borraron " + r.ObjetosBorrados + " contenidos que ningun respaldo usaba (" + Tamanos.Legible(r.BytesLiberados) + ")."));
        }

        private void CambiarDeposito()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "cambiar de deposito")) return;
            string ruta;
            using (var d = new FolderBrowserDialog { Description = "Carpeta del deposito: vacia para crear uno nuevo, o un deposito de Rebith existente" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ruta = d.SelectedPath;
            }
            Seguro(() =>
            {
                Repository nuevo = Repository.AbrirOCrear(ruta);
                if (!Confirmar("Esta PC usara el deposito:\n" + nuevo.Raiz + "\n\nLos respaldos del deposito actual NO se mueven ni se borran. Rebith se reiniciara. Continuar?", "Cambiar de deposito"))
                    return;
                MachineSettings m = RebithService.LeerAjustesLocales();
                m.RepoPath = nuevo.Raiz;
                RebithService.GuardarAjustesLocales(m);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, "cambio_deposito", Servicio.Repo.Raiz + " -> " + nuevo.Raiz);
                Application.Restart();
                Environment.Exit(0);
            });
        }

        // ------------------------------------------------------------------ Programador de tareas

        private static string CorrerSchtasks(string argumentos, out int codigo)
        {
            var psi = new ProcessStartInfo("schtasks.exe", argumentos)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process p = Process.Start(psi))
            {
                string salida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                if (!p.WaitForExit(20000))
                {
                    try { p.Kill(); } catch { }
                    codigo = -1;
                    return "schtasks no respondio.";
                }
                codigo = p.ExitCode;
                return salida.Trim();
            }
        }

        private static string EstadoTarea()
        {
            try
            {
                int codigo;
                CorrerSchtasks("/Query /TN \"" + NombreTarea + "\"", out codigo);
                return codigo == 0 ? "Registrado: los respaldos programados corren aunque la ventana este cerrada." : "No registrado: los respaldos programados solo corren con la ventana abierta.";
            }
            catch
            {
                return "No se pudo consultar el Programador de tareas.";
            }
        }

        private void RegistrarTarea()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "registrar la tarea programada")) return;
            string exe = Application.ExecutablePath;
            string args = "/Create /F /TN \"" + NombreTarea + "\" /SC MINUTE /MO 15 /TR \"\\\"" + exe + "\\\" /programado\" /RL LIMITED";
            Seguro(() =>
            {
                int codigo;
                string salida = CorrerSchtasks(args, out codigo);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, "tarea_programada", codigo == 0 ? "registrada" : "fallo: " + salida);
                if (codigo == 0) Info("Listo. Windows correra Rebith cada 15 minutos para revisar si toca algun respaldo.\n\n" + salida);
                else Aviso("Windows no permitio registrar la tarea (puede requerir permisos o estar bloqueado por politica de TI):\n\n" + salida +
                    "\n\nMientras tanto, los respaldos programados corren con la ventana abierta.");
                AlRefrescar();
            });
        }

        private void QuitarTarea()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "quitar la tarea programada")) return;
            Seguro(() =>
            {
                int codigo;
                string salida = CorrerSchtasks("/Delete /F /TN \"" + NombreTarea + "\"", out codigo);
                Servicio.Auditoria.Registrar(Sesion.ParaAuditoria, "tarea_programada", codigo == 0 ? "quitada" : "fallo al quitar: " + salida);
                if (codigo != 0) Aviso(salida);
                AlRefrescar();
            });
        }

        // ------------------------------------------------------------------ importar

        private void Importar()
        {
            if (!Sesion.Exigir(this, Permiso.GestionarSeguridad, "importar la version anterior")) return;
            string ruta;
            using (var d = new OpenFileDialog { Title = "backups.db de la version anterior", Filter = "backups.db|backups.db|Base de datos|*.db", CheckFileExists = true })
            {
                string predeterminada = Importador.BaseAnteriorPredeterminada();
                if (File.Exists(predeterminada)) d.InitialDirectory = Path.GetDirectoryName(predeterminada);
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ruta = d.FileName;
            }
            string usuario = Sesion.ParaAuditoria;
            Ventana.Ejecutar("Importando la version anterior", (p, c) => new Importador(Servicio).Importar(ruta, usuario, p, c), r =>
            {
                var lineas = new List<string>
                {
                    "Objetivos nuevos: " + r.Objetivos + " (los repetidos de la version anterior se juntaron)",
                    "Respaldos importados: " + r.Respaldos,
                    "Archivos: " + r.Archivos + "   Contenidos copiados y comprobados: " + r.Contenidos,
                    "Usuarios importados: " + r.Usuarios + " (entran con su misma contrasena; el admin del deposito nuevo no se reemplaza)",
                    "Eventos de auditoria: " + r.EventosAuditoria
                };
                lineas.AddRange(r.Avisos.Select(a => "Aviso: " + a));
                using (var d = new DialogoLista("Importacion", r.Avisos.Count == 0 ? "Importacion correcta" : "Importacion con avisos", lineas, r.Avisos.Count == 0 ? Tema.Bien : Tema.Aviso))
                    d.ShowDialog(this);
            });
        }
    }
}
