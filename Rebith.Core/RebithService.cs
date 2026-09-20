using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Rebith.Core
{
    public class ResultadoProgramado
    {
        public List<string> Lineas = new List<string>();
        public bool HuboErrores;
    }

    /// <summary>
    /// Punto de entrada unico para la ventana y para el modo programado. Cada operacion
    /// que cambia el deposito toma el candado, registra auditoria y deja todo en un
    /// estado consistente aunque falle a la mitad.
    /// </summary>
    public class RebithService
    {
        public Repository Repo { get; private set; }
        public Auditoria Auditoria { get; private set; }
        public UserStore Usuarios { get; private set; }

        private RebithService() { }

        // ------------------------------------------------------------------ ajustes de la PC

        /// <summary>Carpeta local de la PC (no del deposito): ubicacion del deposito y logs.</summary>
        public static string CarpetaLocal
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Rebith"); }
        }

        private static string RutaAjustesLocales { get { return Path.Combine(CarpetaLocal, "equipo.json"); } }

        public static string DepositoPredeterminado()
        {
            try
            {
                var d = new DriveInfo("D");
                if (d.IsReady && d.DriveType == DriveType.Fixed) return @"D:\RebithRespaldos";
            }
            catch
            {
            }
            return Path.Combine(CarpetaLocal, "Deposito");
        }

        public static MachineSettings LeerAjustesLocales()
        {
            try
            {
                return JsonFile.Leer<MachineSettings>(RutaAjustesLocales) ?? new MachineSettings();
            }
            catch (Exception ex)
            {
                Log.Error("equipo.json danado", ex);
                return new MachineSettings();
            }
        }

        public static void GuardarAjustesLocales(MachineSettings a)
        {
            JsonFile.Escribir(RutaAjustesLocales, a, true);
        }

        /// <summary>Abre el deposito configurado en esta PC (o el predeterminado).</summary>
        public static RebithService Abrir(string rutaDeposito)
        {
            Log.Configurar(Path.Combine(CarpetaLocal, "logs"));
            var s = new RebithService();
            s.Repo = Repository.AbrirOCrear(rutaDeposito);
            s.Auditoria = new Auditoria(s.Repo.CarpetaAuditoria, s.Repo.Info.RepoId);
            s.Usuarios = new UserStore(s.Repo.CarpetaConfig, s.Auditoria);
            Log.Info("Deposito abierto: " + s.Repo.Raiz);
            return s;
        }

        // ------------------------------------------------------------------ replica

        /// <summary>Abre la replica si esta configurada y disponible. null si no hay o no se puede.</summary>
        public Repository AbrirReplica(out string problema)
        {
            problema = null;
            string ruta = Repo.LeerAjustes().ReplicaPath;
            if (string.IsNullOrWhiteSpace(ruta)) return null;
            try
            {
                if (Rutas.SeTraslapan(ruta, Repo.Raiz))
                {
                    problema = "La replica no puede estar dentro del deposito ni contenerlo.";
                    return null;
                }
                return Repository.AbrirOCrear(ruta);
            }
            catch (Exception ex)
            {
                problema = "La replica no esta disponible (" + ruta + "): " + ex.Message;
                return null;
            }
        }

        private ObjectStore AlmacenReplica()
        {
            string problema;
            Repository r = AbrirReplica(out problema);
            if (problema != null) Log.Advertencia(problema);
            return r == null ? null : r.Objetos;
        }

        /// <summary>
        /// Copia a la replica un respaldo recien hecho (manifiesto + contenido que le falte)
        /// y la configuracion. Nunca lanza excepciones: si falla, devuelve el aviso.
        /// </summary>
        private string CopiarAReplica(SnapshotManifest m, CancellationToken cancelar)
        {
            string problema;
            Repository replica = AbrirReplica(out problema);
            if (replica == null) return problema;
            try
            {
                foreach (string hash in m.Files.Select(f => f.Hash).Distinct())
                {
                    cancelar.ThrowIfCancellationRequested();
                    if (replica.Objetos.Existe(hash)) continue;
                    if (!Repo.Objetos.CopiarA(replica.Objetos, hash, cancelar))
                        return "Un archivo no quedo sano en la replica; se reintentara en la siguiente sincronizacion.";
                }
                replica.GuardarManifiesto(m);
                CopiarConfiguracion(replica);
                return null;
            }
            catch (OperationCanceledException)
            {
                return "Copia a la replica cancelada.";
            }
            catch (Exception ex)
            {
                Log.Error("Copia a replica", ex);
                return "No se pudo copiar a la replica: " + ex.Message;
            }
        }

        private void CopiarConfiguracion(Repository replica)
        {
            foreach (string nombre in new[] { "objetivos.json", "usuarios.json" })
            {
                string origen = Path.Combine(Repo.CarpetaConfig, nombre);
                if (File.Exists(origen))
                    AtomicFile.WriteAllText(Path.Combine(replica.CarpetaConfig, nombre), JsonFile.LeerTextoCompartido(origen), true);
            }
        }

        public class ResultadoSincronizacion
        {
            public int ObjetosCopiados;
            public int RespaldosCopiados;
            public int RespaldosQuitados;
            public int Fallos;
        }

        /// <summary>Deja la replica igual al deposito principal. Nunca borra contenido que el principal use.</summary>
        public ResultadoSincronizacion SincronizarReplica(string usuario, IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            string problema;
            Repository replica = AbrirReplica(out problema);
            if (replica == null) throw new RebithException(problema ?? "No hay replica configurada.");
            var r = new ResultadoSincronizacion();
            using (RepoLock candado = Repo.TomarCandado("Sincronizar replica", usuario))
            using (RepoLock candadoReplica = replica.TomarCandado("Sincronizar replica", usuario))
            {
                Repository.LecturaManifiestos principal = Repo.LeerManifiestos();
                if (principal.Danados.Count > 0)
                    throw new RebithException("Hay manifiestos danados en el deposito principal. Revise la integridad antes de sincronizar.");
                var hashes = principal.Manifiestos.SelectMany(m => m.Files.Select(f => f.Hash)).Distinct().ToList();
                for (int i = 0; i < hashes.Count; i++)
                {
                    cancelar.ThrowIfCancellationRequested();
                    if (progreso != null) progreso.Report(new ProgressInfo { Current = i + 1, Total = hashes.Count, Item = hashes[i].Substring(0, 12), Stage = "Copiando a la replica" });
                    if (replica.Objetos.Existe(hashes[i])) continue;
                    try
                    {
                        if (Repo.Objetos.CopiarA(replica.Objetos, hashes[i], cancelar)) r.ObjetosCopiados++;
                        else r.Fallos++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        r.Fallos++;
                        Log.Advertencia("Replica: " + hashes[i] + ": " + ex.Message);
                    }
                }
                var enReplica = replica.LeerManifiestos();
                var idsPrincipal = new HashSet<string>(principal.Manifiestos.Select(m => m.Id));
                var idsReplica = new HashSet<string>(enReplica.Manifiestos.Select(m => m.Id));
                foreach (SnapshotManifest m in principal.Manifiestos.Where(x => !idsReplica.Contains(x.Id)))
                {
                    replica.GuardarManifiesto(m);
                    r.RespaldosCopiados++;
                }
                if (r.Fallos == 0 && enReplica.Danados.Count == 0)
                {
                    foreach (SnapshotManifest m in enReplica.Manifiestos.Where(x => !idsPrincipal.Contains(x.Id)))
                    {
                        replica.BorrarManifiesto(m.Id);
                        r.RespaldosQuitados++;
                    }
                    replica.LimpiarObjetos(candadoReplica, cancelar);
                }
                CopiarConfiguracion(replica);
            }
            Auditoria.Registrar(usuario, "replica_sincronizada", string.Format("{0} contenidos y {1} respaldos copiados, {2} quitados, {3} fallos",
                r.ObjetosCopiados, r.RespaldosCopiados, r.RespaldosQuitados, r.Fallos));
            return r;
        }

        // ------------------------------------------------------------------ respaldar

        public class ResultadoOperacion
        {
            public ResultadoRespaldo Respaldo;
            public List<string> Avisos = new List<string>();
        }

        public ResultadoOperacion Respaldar(string targetId, string nota, string usuario,
            IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            TargetInfo objetivo = Repo.BuscarObjetivo(targetId);
            if (objetivo == null) throw new RebithException("El objetivo ya no existe.");
            var r = new ResultadoOperacion();
            using (RepoLock candado = Repo.TomarCandado("Respaldo de " + objetivo.Name, usuario))
            {
                try
                {
                    r.Respaldo = new BackupEngine(Repo).Respaldar(candado, objetivo, nota, usuario, progreso, cancelar);
                }
                catch (OperationCanceledException)
                {
                    Auditoria.Registrar(usuario, "respaldo_cancelado", objetivo.Name);
                    throw;
                }
                catch (Exception ex)
                {
                    Auditoria.Registrar(usuario, "respaldo_fallido", objetivo.Name + ": " + ex.Message);
                    throw;
                }
                SnapshotManifest m = r.Respaldo.Manifiesto;
                Auditoria.Registrar(usuario, "respaldo", string.Format("{0} ({1}): {2} archivos, {3} omitidos, {4}",
                    objetivo.Name, m.Id, m.Files.Count, m.Skipped.Count, m.Status));

                if (objetivo.KeepLast > 0)
                {
                    try
                    {
                        int quitados = ConservarUltimosSinCandado(candado, objetivo.Id, objetivo.KeepLast, usuario, cancelar);
                        if (quitados > 0) r.Avisos.Add("Se quitaron " + quitados + " respaldo(s) viejo(s) (se conservan los ultimos " + objetivo.KeepLast + ").");
                    }
                    catch (Exception ex)
                    {
                        r.Avisos.Add("No se pudieron quitar respaldos viejos: " + ex.Message);
                        Log.Error("Retencion", ex);
                    }
                }
            }

            string aviso = CopiarAReplica(r.Respaldo.Manifiesto, CancellationToken.None);
            if (aviso != null) r.Avisos.Add(aviso);
            return r;
        }

        // ------------------------------------------------------------------ borrar

        public int BorrarRespaldos(IList<string> ids, string usuario, CancellationToken cancelar)
        {
            int borrados = 0;
            using (RepoLock candado = Repo.TomarCandado("Borrar respaldos", usuario))
            {
                foreach (string id in ids)
                {
                    SnapshotManifest m = Repo.BuscarRespaldo(id);
                    Repo.BorrarManifiesto(id);
                    borrados++;
                    Auditoria.Registrar(usuario, "respaldo_borrado", id + (m != null ? " (" + m.TargetName + ", " + Tiempo.Legible(m.StartedLocal) + ")" : ""));
                }
                Repository.ResultadoLimpieza limpieza = Repo.LimpiarObjetos(candado, cancelar);
                if (limpieza.Cancelada) Log.Advertencia(limpieza.Motivo);
            }
            return borrados;
        }

        public int ConservarUltimos(string targetId, int cuantos, string usuario, CancellationToken cancelar)
        {
            using (RepoLock candado = Repo.TomarCandado("Conservar ultimos", usuario))
            {
                return ConservarUltimosSinCandado(candado, targetId, cuantos, usuario, cancelar);
            }
        }

        private int ConservarUltimosSinCandado(RepoLock candado, string targetId, int cuantos, string usuario, CancellationToken cancelar)
        {
            if (cuantos < 1) throw new RebithException("Debe conservar al menos 1 respaldo.");
            // El mas nuevo va primero. Solo cuentan los que se pueden leer; los danados no se tocan.
            List<SnapshotManifest> lista = Repo.RespaldosDe(targetId);
            int quitados = 0;
            foreach (SnapshotManifest m in lista.Skip(cuantos))
            {
                Repo.BorrarManifiesto(m.Id);
                quitados++;
                Auditoria.Registrar(usuario, "respaldo_borrado", m.Id + " (retencion: conservar " + cuantos + ")");
            }
            if (quitados > 0)
            {
                Repository.ResultadoLimpieza limpieza = Repo.LimpiarObjetos(candado, cancelar);
                if (limpieza.Cancelada) Log.Advertencia(limpieza.Motivo);
            }
            return quitados;
        }

        public Repository.ResultadoLimpieza Limpiar(string usuario, CancellationToken cancelar)
        {
            using (RepoLock candado = Repo.TomarCandado("Limpieza", usuario))
            {
                Repository.ResultadoLimpieza r = Repo.LimpiarObjetos(candado, cancelar);
                Auditoria.Registrar(usuario, "limpieza", r.Cancelada ? r.Motivo : r.ObjetosBorrados + " contenidos sin uso borrados (" + Tamanos.Legible(r.BytesLiberados) + ")");
                return r;
            }
        }

        // ------------------------------------------------------------------ restaurar

        /// <param name="sobreOriginal">true = escribe en la ubicacion original. Antes hace un respaldo automatico de lo que hay.</param>
        public ResultadoRestauracion Restaurar(string snapshotId, string destino, ICollection<string> rutas, bool sobreOriginal,
            string usuario, IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            SnapshotManifest m = Repo.BuscarRespaldo(snapshotId);
            if (m == null) throw new RebithException("El respaldo ya no existe o esta danado.");
            string previo = "";
            if (sobreOriginal)
            {
                destino = m.IsFolder ? m.SourcePath : Path.GetDirectoryName(m.SourcePath);
                TargetInfo objetivo = Repo.BuscarObjetivo(m.TargetId);
                if (objetivo != null && (Directory.Exists(objetivo.SourcePath) || File.Exists(objetivo.SourcePath)))
                {
                    // Red de seguridad: si la restauracion no era lo que se queria, se puede volver atras.
                    ResultadoOperacion antes = Respaldar(objetivo.Id, "Automatico antes de restaurar " + m.Id, usuario, progreso, cancelar);
                    previo = antes.Respaldo.Manifiesto.Id;
                }
            }
            using (RepoLock candado = Repo.TomarCandado("Restaurar " + m.TargetName, usuario))
            {
                var motor = new RestoreEngine(Repo, AlmacenReplica());
                ResultadoRestauracion r;
                try
                {
                    r = motor.Restaurar(candado, m, destino, rutas, progreso, cancelar);
                }
                catch (Exception ex)
                {
                    Auditoria.Registrar(usuario, "restauracion_fallida", m.Id + " -> " + destino + ": " + ex.Message);
                    throw;
                }
                r.RespaldoPrevioId = previo;
                Auditoria.Registrar(usuario, sobreOriginal ? "restauracion_sobre_original" : "restauracion",
                    string.Format("{0} ({1}) -> {2}: {3} restaurados, {4} iguales, {5} fallos{6}", m.Id, m.TargetName, r.Destino,
                        r.Restaurados, r.SinCambios, r.Fallos.Count, previo.Length > 0 ? ", respaldo previo " + previo : ""));
                return r;
            }
        }

        // ------------------------------------------------------------------ verificar

        public ResultadoVerificacion CompararConOrigen(string snapshotId, bool exacta, string usuario,
            IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            SnapshotManifest m = Repo.BuscarRespaldo(snapshotId);
            if (m == null) throw new RebithException("El respaldo ya no existe o esta danado.");
            ResultadoVerificacion r = new Verifier(Repo, null).CompararConOrigen(m, Repo.BuscarObjetivo(m.TargetId), exacta, progreso, cancelar);
            Auditoria.Registrar(usuario, "verificacion_origen", m.TargetName + " (" + m.Id + "): " + r.Resumen());
            return r;
        }

        /// <param name="snapshotId">null = todo el deposito.</param>
        public ResultadoVerificacion RevisarIntegridad(string snapshotId, bool repararDesdeReplica, int saltarVerificadosDias,
            string usuario, IProgress<ProgressInfo> progreso, CancellationToken cancelar)
        {
            using (RepoLock candado = Repo.TomarCandado("Revision de integridad", usuario))
            {
                Repository.LecturaManifiestos lectura = Repo.LeerManifiestos();
                List<SnapshotManifest> lista;
                Dictionary<string, string> danados;
                if (snapshotId == null)
                {
                    lista = lectura.Manifiestos;
                    danados = lectura.Danados;
                }
                else
                {
                    lista = lectura.Manifiestos.Where(m => m.Id == snapshotId).ToList();
                    if (lista.Count == 0) throw new RebithException("El respaldo ya no existe o esta danado.");
                    danados = null;
                }
                ResultadoVerificacion r = new Verifier(Repo, AlmacenReplica()).RevisarIntegridad(candado, lista, danados,
                    repararDesdeReplica, saltarVerificadosDias, progreso, cancelar);
                Auditoria.Registrar(usuario, "verificacion_integridad", (snapshotId ?? "todo el deposito") + ": " + r.Resumen());
                return r;
            }
        }

        // ------------------------------------------------------------------ programado

        /// <summary>
        /// Corre todo lo que toca: respaldos programados y la revision automatica.
        /// Un objetivo que falla no impide los demas. Si el deposito esta ocupado, sale sin error.
        /// </summary>
        public ResultadoProgramado EjecutarPendientes(bool alIniciar, CancellationToken cancelar)
        {
            var r = new ResultadoProgramado();
            const string Usuario = "programado";
            List<TargetInfo> objetivos;
            try
            {
                objetivos = Repo.LeerObjetivos();
            }
            catch (Exception ex)
            {
                r.HuboErrores = true;
                r.Lineas.Add("No se pudieron leer los objetivos: " + ex.Message);
                return r;
            }

            DateTime ahora = DateTime.Now;
            foreach (TargetInfo t in objetivos)
            {
                if (cancelar.IsCancellationRequested) break;
                bool toca = Programador.TocaAhora(t, ahora) ||
                    (alIniciar && t.Schedule != null && t.Schedule.RunOnStartup && t.Schedule.Frequency != Frecuencias.Manual);
                if (!toca) continue;
                try
                {
                    ResultadoOperacion op = Respaldar(t.Id, "Respaldo programado", Usuario, null, cancelar);
                    SnapshotManifest m = op.Respaldo.Manifiesto;
                    string linea = t.Name + ": " + m.Status + ", " + m.Files.Count + " archivos" + (m.Skipped.Count > 0 ? ", " + m.Skipped.Count + " omitidos" : "");
                    foreach (string aviso in op.Avisos) linea += ". " + aviso;
                    r.Lineas.Add(linea);
                    Repo.AnotarEjecucionProgramada(t.Id, m.Status);
                }
                catch (RepoBusyException ex)
                {
                    r.Lineas.Add(t.Name + ": se intentara despues (" + ex.Message + ")");
                }
                catch (OperationCanceledException)
                {
                    r.Lineas.Add(t.Name + ": cancelado.");
                    break;
                }
                catch (Exception ex)
                {
                    r.HuboErrores = true;
                    r.Lineas.Add(t.Name + ": ERROR " + ex.Message);
                    Log.Error("Respaldo programado de " + t.Name, ex);
                    // Se reintenta en una hora; el error queda visible en Inicio.
                    try { Repo.AnotarFalloProgramado(t.Id, ex.Message); } catch { }
                }
            }

            try
            {
                RepoSettings ajustes = Repo.LeerAjustes();
                if (!cancelar.IsCancellationRequested && Programador.TocaRevisionAutomatica(ajustes, DateTime.Now))
                {
                    ResultadoVerificacion v = RevisarIntegridad(null, true, ajustes.AutoVerifyDays, Usuario, null, cancelar);
                    string reporte = Verifier.GuardarReporte(v, Repo.CarpetaReportes, "integridad_automatica");
                    ajustes = Repo.LeerAjustes();
                    ajustes.LastAutoVerifyLocal = Tiempo.AhoraIso();
                    ajustes.LastAutoVerifyOk = v.TodoBien;
                    ajustes.LastAutoVerifyResult = v.Resumen() + " | Reporte: " + reporte;
                    Repo.GuardarAjustes(ajustes);
                    r.Lineas.Add("Revision automatica de integridad: " + v.Resumen());
                    if (!v.TodoBien) r.HuboErrores = true;
                }
            }
            catch (RepoBusyException)
            {
                r.Lineas.Add("Revision automatica: deposito ocupado, se intentara despues.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                r.HuboErrores = true;
                r.Lineas.Add("Revision automatica: ERROR " + ex.Message);
                Log.Error("Revision automatica", ex);
            }

            foreach (string linea in r.Lineas) Log.Info("Programado: " + linea);
            return r;
        }
    }
}
