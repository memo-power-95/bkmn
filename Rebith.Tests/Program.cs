using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Rebith.Core;

namespace Rebith.Tests
{
    /// <summary>
    /// Pruebas del motor sin librerias externas: dotnet run --project Rebith.Tests
    /// Cada prueba trabaja en su propia carpeta temporal y se borra al final.
    /// </summary>
    internal static class Program
    {
        private static int fallas;
        private static int pasadas;

        private static int Main()
        {
            Log.Configurar(Path.Combine(Path.GetTempPath(), "rebith_tests_logs"));
            Correr("Respaldo y restauracion completos", RespaldoYRestauracion);
            Correr("Deduplicado", Deduplicado);
            Correr("Compatibilidad con .bin de Python", CompatibilidadPython);
            Correr("Contenido danado se detecta y se cura en el siguiente respaldo", DanadoYCurado);
            Correr("Contenido faltante se detecta", ObjetoFaltante);
            Correr("Manifiesto alterado: se detecta y la limpieza no borra nada", ManifiestoAlterado);
            Correr("Comparar con origen: OK / MODIFICADO / FALTA / NUEVO", CompararConOrigen);
            Correr("Exclusiones estilo fnmatch", Exclusiones_);
            Correr("Programador: proximo turno", ProgramadorTurnos);
            Correr("Contrasenas: PBKDF2 y formato de Python", ContrasenasPrueba);
            Correr("Usuarios: bloqueo por intentos y ultimo admin", UsuariosPrueba);
            Correr("Auditoria encadenada detecta alteraciones", AuditoriaPrueba);
            Correr("Retencion y limpieza de contenido sin uso", RetencionPrueba);
            Correr("Rutas peligrosas bloqueadas al restaurar", RutasPeligrosas);
            Correr("Archivo grande por partes (60 MB)", ArchivoGrande);
            Correr("Replica repara contenido danado", ReplicaRepara);
            Correr("Restaurar desde replica si el principal esta danado", RestaurarDesdeReplica);
            Correr("Cancelar no deja respaldo registrado", CancelarRespaldo);
            Correr("Candado: dos operaciones no chocan", CandadoPrueba);
            Correr("Reutiliza archivos sin cambios (incremental)", Incremental);
            Correr("JSON danado usa la copia .bak", JsonBak);
            Correr("Objetivos: no se repiten ni contienen al deposito", ObjetivosValidacion);
            Correr("Restaurar sobre original hace respaldo previo", RestaurarSobreOriginal);
            Correr("Programado: corre lo pendiente y reintenta tras error", ProgramadoPrueba);
            Correr("Importar respaldos hechos por la version en Python", ImportarPython);

            Console.WriteLine();
            Console.WriteLine("Pasaron {0}, fallaron {1}", pasadas, fallas);
            return fallas == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ utilidades

        private static void Correr(string nombre, Action<Carpeta> prueba)
        {
            var c = new Carpeta();
            try
            {
                prueba(c);
                pasadas++;
                Console.WriteLine("  OK    " + nombre);
            }
            catch (Exception ex)
            {
                fallas++;
                Console.WriteLine("  FALLA " + nombre + ": " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                c.Borrar();
            }
        }

        private static void Afirmar(bool condicion, string mensaje)
        {
            if (!condicion) throw new Exception("Se esperaba: " + mensaje);
        }

        private class Carpeta
        {
            public readonly string Raiz = Path.Combine(Path.GetTempPath(), "rebith_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            public string Origen { get { return Path.Combine(Raiz, "origen"); } }
            public string Deposito { get { return Path.Combine(Raiz, "deposito"); } }
            public string Replica { get { return Path.Combine(Raiz, "replica"); } }
            public string Destino { get { return Path.Combine(Raiz, "destino"); } }

            public Carpeta()
            {
                Directory.CreateDirectory(Origen);
            }

            public void Escribir(string rel, string texto)
            {
                string ruta = Path.Combine(Origen, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                File.WriteAllText(ruta, texto);
            }

            public void Borrar()
            {
                try { Directory.Delete(Raiz, true); } catch { }
            }
        }

        private static RebithService Servicio(Carpeta c)
        {
            return RebithService.Abrir(c.Deposito);
        }

        private static TargetInfo Objetivo(RebithService s, Carpeta c, string nombre = "Maquina")
        {
            return s.Repo.GuardarObjetivo(new TargetInfo { Name = nombre, SourcePath = c.Origen, IsFolder = true });
        }

        private static SnapshotManifest Respaldar(RebithService s, TargetInfo t)
        {
            return s.Respaldar(t.Id, "prueba", "tester", null, CancellationToken.None).Respaldo.Manifiesto;
        }

        private static string Sha(string ruta)
        {
            return Verifier.HashDeArchivo(ruta, CancellationToken.None);
        }

        private static void Envejecer(string carpeta)
        {
            foreach (string f in Directory.GetFiles(carpeta, "*.bin", SearchOption.AllDirectories))
                File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddDays(-2));
        }

        private static void DanarObjeto(RebithService s, string hash)
        {
            string ruta = s.Repo.Objetos.RutaDe(hash);
            byte[] b = File.ReadAllBytes(ruta);
            b[b.Length / 2] ^= 0xFF;
            File.WriteAllBytes(ruta, b);
        }

        // ------------------------------------------------------------------ pruebas

        private static void RespaldoYRestauracion(Carpeta c)
        {
            c.Escribir("config/maquina.xml", "<a>1</a>");
            c.Escribir("ModuleData/GrabPosition.xml", "posiciones");
            c.Escribir("vacio.txt", "");
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            SnapshotManifest m = Respaldar(s, t);
            Afirmar(m.Files.Count == 3, "3 archivos respaldados, hay " + m.Files.Count);
            Afirmar(m.Status == EstadosSnapshot.Completo, "estado Completo");

            ResultadoRestauracion r = s.Restaurar(m.Id, c.Destino, null, false, "tester", null, CancellationToken.None);
            Afirmar(r.Fallos.Count == 0 && r.Restaurados == 3, "3 restaurados sin fallos");
            foreach (FileEntry f in m.Files)
            {
                string restaurado = Path.Combine(c.Destino, f.Path.Replace('/', Path.DirectorySeparatorChar));
                Afirmar(Sha(restaurado) == f.Hash, "misma huella en " + f.Path);
                Afirmar(File.GetLastWriteTimeUtc(restaurado).Ticks == f.MtimeUtcTicks, "misma fecha en " + f.Path);
            }
            ResultadoRestauracion otra = s.Restaurar(m.Id, c.Destino, null, false, "tester", null, CancellationToken.None);
            Afirmar(otra.SinCambios == 3 && otra.Restaurados == 0, "segunda restauracion no reescribe nada");
        }

        private static void Deduplicado(Carpeta c)
        {
            c.Escribir("a.txt", "mismo contenido");
            c.Escribir("b/c.txt", "mismo contenido");
            RebithService s = Servicio(c);
            SnapshotManifest m = Respaldar(s, Objetivo(s, c));
            Afirmar(m.NewObjects == 1, "1 solo contenido guardado, hubo " + m.NewObjects);
            Afirmar(Directory.GetFiles(s.Repo.Objetos.Raiz, "*.bin", SearchOption.AllDirectories).Length == 1, "1 .bin en disco");
        }

        private static void CompatibilidadPython(Carpeta c)
        {
            RebithService s = Servicio(c);
            const string hash = "ee24369bd24a6c5ebfd7c781ee479679ed01143edcf19fd576a9c04eef4be1b1";
            const string zlibHex = "789ccbc8cf495448492d4e495528a82cc9c8cfd351284a4dca2cc95048cecf2d482cc94cca49e5caa09a2200f89828de";
            string ruta = s.Repo.Objetos.RutaDe(hash);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            byte[] bytes = Enumerable.Range(0, zlibHex.Length / 2).Select(i => Convert.ToByte(zlibHex.Substring(i * 2, 2), 16)).ToArray();
            File.WriteAllBytes(ruta, bytes);
            ResultadoObjeto r = s.Repo.Objetos.Verificar(hash, -1, CancellationToken.None);
            Afirmar(r.Estado == EstadoObjeto.Ok, "el .bin de Python se lee y su huella coincide (" + r.Detalle + ")");

            // Y al reves: lo que escribe C# debe tener un zlib valido (Adler-32 incluido).
            c.Escribir("x.txt", "hola desde python, rebith compatible\n");
            ObjetoGuardado g = s.Repo.Objetos.GuardarDesdeArchivo(Path.Combine(c.Origen, "x.txt"), null, CancellationToken.None);
            byte[] escrito = File.ReadAllBytes(s.Repo.Objetos.RutaDe(g.Hash));
            Afirmar(escrito[0] == 0x78, "encabezado zlib");
            Afirmar(ZlibValido(escrito, File.ReadAllBytes(Path.Combine(c.Origen, "x.txt"))), "Adler-32 correcto (Python lo podra leer)");
        }

        private static bool ZlibValido(byte[] zlib, byte[] original)
        {
            uint a = 1, b = 0;
            foreach (byte x in original) { a = (a + x) % 65521; b = (b + a) % 65521; }
            uint esperado = (b << 16) | a;
            int n = zlib.Length;
            uint escrito = ((uint)zlib[n - 4] << 24) | ((uint)zlib[n - 3] << 16) | ((uint)zlib[n - 2] << 8) | zlib[n - 1];
            return esperado == escrito;
        }

        private static void DanadoYCurado(Carpeta c)
        {
            c.Escribir("receta.vpp", new string('x', 5000) + "fin");
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            SnapshotManifest m = Respaldar(s, t);
            string hash = m.Files[0].Hash;
            DanarObjeto(s, hash);

            ResultadoVerificacion v = s.RevisarIntegridad(null, false, 0, "tester", null, CancellationToken.None);
            Afirmar(v.Cuenta(EstadosVerificacion.Danado) == 1, "1 danado, resumen: " + v.Resumen());
            Afirmar(!v.TodoBien, "no todo bien");

            SnapshotManifest m2 = Respaldar(s, t);
            Afirmar(m2.Files[0].Hash == hash, "mismo contenido");
            ResultadoVerificacion v2 = s.RevisarIntegridad(null, false, 0, "tester", null, CancellationToken.None);
            Afirmar(v2.TodoBien, "curado tras respaldar de nuevo: " + v2.Resumen());
        }

        private static void ObjetoFaltante(Carpeta c)
        {
            c.Escribir("a.txt", "aaa");
            RebithService s = Servicio(c);
            SnapshotManifest m = Respaldar(s, Objetivo(s, c));
            File.Delete(s.Repo.Objetos.RutaDe(m.Files[0].Hash));
            ResultadoVerificacion v = s.RevisarIntegridad(m.Id, false, 0, "tester", null, CancellationToken.None);
            Afirmar(v.Cuenta(EstadosVerificacion.FaltaEnDeposito) == 1, "FALTA EN DEPOSITO: " + v.Resumen());
        }

        private static void ManifiestoAlterado(Carpeta c)
        {
            c.Escribir("a.txt", "aaa");
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            SnapshotManifest m = Respaldar(s, t);
            c.Escribir("b.txt", "bbb");
            SnapshotManifest m2 = Respaldar(s, t);
            string ruta = Path.Combine(s.Repo.CarpetaRespaldos, m.Id + ".json");
            File.WriteAllText(ruta, File.ReadAllText(ruta).Replace("a.txt", "z.txt"));

            Repository.LecturaManifiestos l = s.Repo.LeerManifiestos();
            Afirmar(l.Danados.Count == 1, "1 manifiesto danado");
            ResultadoVerificacion v = s.RevisarIntegridad(null, false, 0, "tester", null, CancellationToken.None);
            Afirmar(v.Cuenta(EstadosVerificacion.ManifiestoDanado) == 1, "se reporta MANIFIESTO DANADO");

            Envejecer(s.Repo.Objetos.Raiz);
            s.BorrarRespaldos(new[] { m2.Id }, "tester", CancellationToken.None);
            Afirmar(Directory.GetFiles(s.Repo.Objetos.Raiz, "*.bin", SearchOption.AllDirectories).Length == 2, "no se borro contenido (hay un manifiesto ilegible)");
        }

        private static void CompararConOrigen(Carpeta c)
        {
            c.Escribir("igual.txt", "1");
            c.Escribir("cambia.txt", "original");
            c.Escribir("se_borra.txt", "x");
            c.Escribir("fecha.txt", "misma");
            RebithService s = Servicio(c);
            SnapshotManifest m = Respaldar(s, Objetivo(s, c));
            c.Escribir("cambia.txt", "modificado!");
            File.Delete(Path.Combine(c.Origen, "se_borra.txt"));
            c.Escribir("nuevo/otro.txt", "n");
            File.SetLastWriteTimeUtc(Path.Combine(c.Origen, "fecha.txt"), DateTime.UtcNow.AddDays(-10));

            ResultadoVerificacion v = s.CompararConOrigen(m.Id, false, "tester", null, CancellationToken.None);
            Func<string, string> estado = ruta => v.Items.First(i => i.Ruta == ruta).Estado;
            Afirmar(estado("igual.txt") == EstadosVerificacion.Ok, "igual OK");
            Afirmar(estado("cambia.txt") == EstadosVerificacion.Modificado, "cambia MODIFICADO");
            Afirmar(estado("se_borra.txt") == EstadosVerificacion.Falta, "se_borra FALTA");
            Afirmar(estado("nuevo/otro.txt") == EstadosVerificacion.Nuevo, "nuevo NUEVO");
            Afirmar(estado("fecha.txt") == EstadosVerificacion.Ok, "solo fecha distinta = OK por huella");

            c.Escribir("igual.txt", "2");
            File.SetLastWriteTimeUtc(Path.Combine(c.Origen, "igual.txt"), new DateTime(m.Files.First(f => f.Path == "igual.txt").MtimeUtcTicks, DateTimeKind.Utc));
            ResultadoVerificacion rapida = s.CompararConOrigen(m.Id, false, "tester", null, CancellationToken.None);
            ResultadoVerificacion exacta = s.CompararConOrigen(m.Id, true, "tester", null, CancellationToken.None);
            Afirmar(exacta.Items.First(i => i.Ruta == "igual.txt").Estado == EstadosVerificacion.Modificado, "la exacta detecta cambio con misma fecha y tamano");
            Afirmar(rapida.Items.First(i => i.Ruta == "igual.txt").Estado == EstadosVerificacion.Ok, "la rapida no lo ve (por eso existe la exacta)");
        }

        private static void Exclusiones_(Carpeta c)
        {
            var e = new Exclusiones(new[] { "*.log", "cache/*", "Temp", "file?.tmp" });
            Afirmar(e.Excluye("a/b/x.log"), "*.log en subcarpeta");
            Afirmar(e.Excluye("cache/uno/dos.bin"), "cache/*");
            Afirmar(e.Excluye("algo/Temp"), "nombre exacto de carpeta");
            Afirmar(e.Excluye("FILE1.TMP"), "sin distinguir mayusculas y ?");
            Afirmar(!e.Excluye("config.xml"), "no excluye lo demas");
            Afirmar(!e.Excluye("file12.tmp"), "? es un solo caracter");

            c.Escribir("a.log", "x");
            c.Escribir("Temp/b.txt", "x");
            c.Escribir("c.txt", "x");
            RebithService s = Servicio(c);
            TargetInfo t = s.Repo.GuardarObjetivo(new TargetInfo { Name = "M", SourcePath = c.Origen, IsFolder = true, Excludes = new List<string> { "*.log", "Temp" } });
            SnapshotManifest m = Respaldar(s, t);
            Afirmar(m.Files.Count == 1 && m.Files[0].Path == "c.txt", "solo c.txt respaldado");
        }

        private static void ProgramadorTurnos(Carpeta c)
        {
            var t = new TargetInfo { CreatedLocal = "2026-09-18T10:00:00", Schedule = new ScheduleInfo { Frequency = Frecuencias.Diario, Hour = 2, Minute = 30 } };
            DateTime? p = Programador.ProximaEjecucion(t, new DateTime(2026, 9, 18, 11, 0, 0));
            Afirmar(p == new DateTime(2026, 9, 19, 2, 30, 0), "diario: manana 02:30, fue " + p);
            Afirmar(!Programador.TocaAhora(t, new DateTime(2026, 9, 19, 2, 29, 0)), "no antes de la hora");
            Afirmar(Programador.TocaAhora(t, new DateTime(2026, 9, 20, 8, 0, 0)), "PC apagada: corre al volver");
            t.Schedule.LastRunLocal = "2026-09-20T08:00:00";
            Afirmar(Programador.ProximaEjecucion(t, DateTime.Now) == new DateTime(2026, 9, 21, 2, 30, 0), "tras correr tarde, vuelve a su hora");

            var w = new TargetInfo { CreatedLocal = "2026-09-18T10:00:00", Schedule = new ScheduleInfo { Frequency = Frecuencias.Semanal, Hour = 3, Minute = 0, DayOfWeek = 1 } };
            Afirmar(Programador.ProximaEjecucion(w, DateTime.Now) == new DateTime(2026, 9, 21, 3, 0, 0), "semanal: el lunes siguiente");
            w.Schedule.RetryAfterLocal = "2026-09-21T04:00:00";
            Afirmar(!Programador.TocaAhora(w, new DateTime(2026, 9, 21, 3, 30, 0)), "respeta el reintento");
            Afirmar(Programador.TocaAhora(w, new DateTime(2026, 9, 21, 4, 1, 0)), "reintenta despues");
            Afirmar(Programador.ProximaEjecucion(new TargetInfo(), DateTime.Now) == null, "manual no se programa");
        }

        private static void ContrasenasPrueba(Carpeta c)
        {
            string h = Contrasenas.Crear("Secreta#1");
            bool viejo;
            Afirmar(Contrasenas.Verificar("Secreta#1", h, out viejo) && !viejo, "PBKDF2 correcta");
            Afirmar(!Contrasenas.Verificar("secreta#1", h, out viejo), "distingue mayusculas");
            Afirmar(Contrasenas.Crear("Secreta#1") != h, "sal distinta cada vez");
            Afirmar(Contrasenas.Verificar("admin123", "sha256$240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9", out viejo) && viejo, "formato de Python");
            Afirmar(Contrasenas.ValidarNueva("123") != null, "rechaza cortas");
        }

        private static void UsuariosPrueba(Carpeta c)
        {
            RebithService s = Servicio(c);
            ResultadoLogin l = s.Usuarios.Entrar("admin", UserStore.ContrasenaInicial);
            Afirmar(l.Usuario != null && l.DebeCambiarContrasena, "admin inicial entra y debe cambiar contrasena");
            UserInfo admin = l.Usuario;
            s.Usuarios.Guardar(admin, null, new UserInfo { Username = "oper", Role = Roles.Operador, IsActive = true, CanBackup = true }, "oper123");
            for (int i = 0; i < UserStore.IntentosMaximos; i++) s.Usuarios.Entrar("oper", "mala");
            ResultadoLogin bloqueado = s.Usuarios.Entrar("oper", "oper123");
            Afirmar(bloqueado.Usuario == null && bloqueado.Error.Contains("bloqueado"), "bloqueado tras 5 intentos");
            UserInfo oper = s.Usuarios.Refrescar("oper");
            Afirmar(UserStore.Tiene(oper, Permiso.Respaldar) && !UserStore.Tiene(oper, Permiso.Borrar), "permisos del operador");

            bool fallo = false;
            try
            {
                UserInfo datos = admin.Clonar();
                datos.IsActive = false;
                s.Usuarios.Guardar(admin, "admin", datos, null);
            }
            catch (RebithException) { fallo = true; }
            Afirmar(fallo, "no deja desactivar al ultimo administrador");
            bool sinPermiso = false;
            try { s.Usuarios.Eliminar(oper, "admin"); } catch (RebithException) { sinPermiso = true; }
            Afirmar(sinPermiso, "un operador no administra usuarios");
        }

        private static void AuditoriaPrueba(Carpeta c)
        {
            RebithService s = Servicio(c);
            s.Auditoria.Registrar("a", "uno", "detalle 1");
            s.Auditoria.Registrar("b", "dos", "detalle 2");
            s.Auditoria.Registrar("c", "tres", "detalle 3");
            Afirmar(s.Auditoria.VerificarCadena().Integra, "cadena integra");
            string[] lineas = File.ReadAllLines(s.Auditoria.Archivo);
            File.WriteAllLines(s.Auditoria.Archivo, lineas.Where(x => !x.Contains("detalle 2")).ToArray());
            ResultadoCadena r = s.Auditoria.VerificarCadena();
            Afirmar(!r.Integra, "detecta la linea borrada: " + r.Detalle);
        }

        private static void RetencionPrueba(Carpeta c)
        {
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            for (int i = 0; i < 4; i++)
            {
                c.Escribir("dato.txt", "version " + i);
                Respaldar(s, t);
                Thread.Sleep(20);
            }
            Afirmar(s.Repo.RespaldosDe(t.Id).Count == 4, "4 respaldos");
            Envejecer(s.Repo.Objetos.Raiz);
            int quitados = s.ConservarUltimos(t.Id, 2, "tester", CancellationToken.None);
            Afirmar(quitados == 2 && s.Repo.RespaldosDe(t.Id).Count == 2, "quedan 2");
            Afirmar(Directory.GetFiles(s.Repo.Objetos.Raiz, "*.bin", SearchOption.AllDirectories).Length == 2, "solo el contenido en uso");
            Afirmar(s.Repo.RespaldosDe(t.Id)[0].Files[0].Hash == Sha(Path.Combine(c.Origen, "dato.txt")), "se conserva el mas nuevo");
        }

        private static void RutasPeligrosas(Carpeta c)
        {
            foreach (string mala in new[] { "../fuera.txt", "a/../../fuera.txt", "/etc/passwd", "C:/Windows/x.dll", "" })
            {
                bool bloqueada = false;
                try { Rutas.UnirSegura(c.Destino, mala); } catch (RebithException) { bloqueada = true; }
                Afirmar(bloqueada, "bloquea " + mala);
            }
            Afirmar(Rutas.UnirSegura(c.Destino, "a/b.txt").StartsWith(Path.GetFullPath(c.Destino)), "permite rutas normales");
        }

        private static void ArchivoGrande(Carpeta c)
        {
            string ruta = Path.Combine(c.Origen, "grande.vpp");
            var rnd = new Random(7);
            byte[] bloque = new byte[1024 * 1024];
            using (var fs = File.Create(ruta))
            {
                for (int i = 0; i < 60; i++)
                {
                    rnd.NextBytes(bloque);
                    fs.Write(bloque, 0, bloque.Length);
                }
            }
            long memoriaAntes = GC.GetTotalMemory(true);
            RebithService s = Servicio(c);
            SnapshotManifest m = Respaldar(s, Objetivo(s, c));
            long memoriaDespues = GC.GetTotalMemory(true);
            Afirmar(m.Files[0].Size == 60L * 1024 * 1024, "tamano completo");
            Afirmar(memoriaDespues - memoriaAntes < 30L * 1024 * 1024, "no carga el archivo entero en memoria");
            s.Restaurar(m.Id, c.Destino, null, false, "tester", null, CancellationToken.None);
            Afirmar(Sha(Path.Combine(c.Destino, "grande.vpp")) == m.Files[0].Hash, "restaurado identico");
        }

        private static void ConReplica(RebithService s, Carpeta c)
        {
            RepoSettings a = s.Repo.LeerAjustes();
            a.ReplicaPath = c.Replica;
            s.Repo.GuardarAjustes(a);
        }

        private static void ReplicaRepara(Carpeta c)
        {
            c.Escribir("a.txt", "contenido importante");
            RebithService s = Servicio(c);
            ConReplica(s, c);
            SnapshotManifest m = Respaldar(s, Objetivo(s, c));
            string enReplica = Path.Combine(c.Replica, "objetos", m.Files[0].Hash.Substring(0, 2), m.Files[0].Hash + ".bin");
            Afirmar(File.Exists(enReplica), "la replica recibio el contenido");
            Afirmar(File.Exists(Path.Combine(c.Replica, "respaldos", m.Id + ".json")), "la replica recibio el manifiesto");
            DanarObjeto(s, m.Files[0].Hash);
            ResultadoVerificacion v = s.RevisarIntegridad(null, true, 0, "tester", null, CancellationToken.None);
            Afirmar(v.Cuenta(EstadosVerificacion.Reparado) == 1, "REPARADO desde replica: " + v.Resumen());
            Afirmar(s.Repo.Objetos.Verificar(m.Files[0].Hash, -1, CancellationToken.None).Estado == EstadoObjeto.Ok, "quedo sano");
        }

        private static void RestaurarDesdeReplica(Carpeta c)
        {
            c.Escribir("a.txt", "contenido importante");
            RebithService s = Servicio(c);
            ConReplica(s, c);
            SnapshotManifest m = Respaldar(s, Objetivo(s, c));
            DanarObjeto(s, m.Files[0].Hash);
            ResultadoRestauracion r = s.Restaurar(m.Id, c.Destino, null, false, "tester", null, CancellationToken.None);
            Afirmar(r.Fallos.Count == 0, "sin fallos");
            Afirmar(Sha(Path.Combine(c.Destino, "a.txt")) == m.Files[0].Hash, "restaurado bien desde la replica");
        }

        private static void CancelarRespaldo(Carpeta c)
        {
            for (int i = 0; i < 50; i++) c.Escribir("f" + i + ".txt", "dato " + i);
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            var cts = new CancellationTokenSource();
            var progreso = new ProgresoSincrono(p => { if (p.Current == 10) cts.Cancel(); });
            bool cancelado = false;
            try { s.Respaldar(t.Id, "", "tester", progreso, cts.Token); } catch (OperationCanceledException) { cancelado = true; }
            Afirmar(cancelado, "se cancelo");
            Afirmar(s.Repo.RespaldosDe(t.Id).Count == 0, "no quedo respaldo registrado");
            Afirmar(Directory.GetFiles(s.Repo.CarpetaRespaldos, "*.encurso").Length == 0, "no quedo marca en curso");
            SnapshotManifest m = Respaldar(s, t);
            Afirmar(m.Files.Count == 50, "el siguiente respaldo sale completo");
        }

        private class ProgresoSincrono : IProgress<ProgressInfo>
        {
            private readonly Action<ProgressInfo> accion;
            public ProgresoSincrono(Action<ProgressInfo> accion) { this.accion = accion; }
            public void Report(ProgressInfo value) { accion(value); }
        }

        private static void CandadoPrueba(Carpeta c)
        {
            RebithService s = Servicio(c);
            using (s.Repo.TomarCandado("prueba", "a"))
            {
                bool ocupado = false;
                try
                {
                    using (s.Repo.TomarCandado("otra", "b")) { }
                }
                catch (RepoBusyException ex)
                {
                    ocupado = ex.Message.Contains("prueba");
                }
                Afirmar(ocupado, "la segunda operacion ve el deposito ocupado y quien lo tiene");
            }
            using (s.Repo.TomarCandado("despues", "c")) { }
        }

        private static void Incremental(Carpeta c)
        {
            c.Escribir("a.txt", "aaa");
            c.Escribir("b.txt", "bbb");
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            Respaldar(s, t);
            c.Escribir("b.txt", "bbb cambiado");
            RebithService.ResultadoOperacion r = s.Respaldar(t.Id, "", "tester", null, CancellationToken.None);
            Afirmar(r.Respaldo.Reutilizados == 1, "a.txt reutilizado sin leer");
            Afirmar(r.Respaldo.Manifiesto.NewObjects == 1, "solo b.txt nuevo");
        }

        private static void JsonBak(Carpeta c)
        {
            RebithService s = Servicio(c);
            Objetivo(s, c, "Uno");
            s.Repo.GuardarObjetivo(new TargetInfo { Name = "Dos", SourcePath = Path.Combine(c.Raiz, "otra"), IsFolder = true });
            string ruta = Path.Combine(s.Repo.CarpetaConfig, "objetivos.json");
            File.WriteAllText(ruta, "{ esto no es json");
            List<TargetInfo> lista = s.Repo.LeerObjetivos();
            Afirmar(lista.Count == 1 && lista[0].Name == "Uno", "uso la version anterior (.bak)");
        }

        private static void ObjetivosValidacion(Carpeta c)
        {
            RebithService s = Servicio(c);
            Objetivo(s, c, "Uno");
            bool repetido = false, dentro = false;
            try { Objetivo(s, c, "Otro nombre"); } catch (RebithException) { repetido = true; }
            try { s.Repo.GuardarObjetivo(new TargetInfo { Name = "Raiz", SourcePath = c.Raiz, IsFolder = true }); } catch (RebithException) { dentro = true; }
            Afirmar(repetido, "misma ruta dos veces");
            Afirmar(dentro, "carpeta que contiene al deposito");
        }

        private static void RestaurarSobreOriginal(Carpeta c)
        {
            c.Escribir("a.txt", "bueno");
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            SnapshotManifest bueno = Respaldar(s, t);
            c.Escribir("a.txt", "lo cambiaron mal");
            ResultadoRestauracion r = s.Restaurar(bueno.Id, null, null, true, "tester", null, CancellationToken.None);
            Afirmar(File.ReadAllText(Path.Combine(c.Origen, "a.txt")) == "bueno", "se restauro en su lugar");
            Afirmar(r.RespaldoPrevioId.Length > 0, "hubo respaldo previo");
            SnapshotManifest previo = s.Repo.BuscarRespaldo(r.RespaldoPrevioId);
            s.Restaurar(previo.Id, c.Destino, null, false, "tester", null, CancellationToken.None);
            Afirmar(File.ReadAllText(Path.Combine(c.Destino, "a.txt")) == "lo cambiaron mal", "el estado anterior se puede recuperar");
        }

        /// <summary>Usa el codigo Python original (../backup_manager) para crear backups.db y storage.</summary>
        private static void ImportarPython(Carpeta c)
        {
            string python = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backup_manager"));
            if (!File.Exists(Path.Combine(python, "backup_engine.py"))) throw new Exception("No se encontro el proyecto Python en " + python);
            c.Escribir("receta.xml", "<receta>1</receta>");
            c.Escribir("sub/datos.ini", new string('z', 20000));
            c.Escribir("sub/copia.ini", new string('z', 20000));
            string viejo = Path.Combine(c.Raiz, "viejo");
            Directory.CreateDirectory(viejo);
            string script = string.Join("\n", new[]
            {
                "import sys, time",
                "sys.path.insert(0, r'" + python + "')",
                "from database import Database",
                "from backup_engine import BackupEngine",
                "db = Database(r'" + Path.Combine(viejo, "backups.db") + "')",
                "eng = BackupEngine(db, storage_dir=r'" + Path.Combine(viejo, "storage") + "')",
                "t = db.create_target('Maquina', r'" + c.Origen + "', True)",
                "eng.backup_target(t, 'primero')",
                "open(r'" + Path.Combine(c.Origen, "receta.xml") + "', 'w').write('<receta>2</receta>')",
                "time.sleep(0.05)",
                "t2 = db.create_target('Maquina', r'" + c.Origen + "', True)",
                "eng.backup_target(t2, 'segundo')",
                "db.create_user('oper', 'clave123')",
                "db.log_event(None, 'prueba', 'evento viejo')",
                "db.close()"
            });
            string archivoScript = Path.Combine(c.Raiz, "crear.py");
            File.WriteAllText(archivoScript, script);
            var psi = new System.Diagnostics.ProcessStartInfo("python3", "\"" + archivoScript + "\"") { UseShellExecute = false, RedirectStandardError = true };
            psi.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0) throw new Exception("Python fallo: " + error);
            }

            RebithService s = Servicio(c);
            ResultadoImportacion r = new Importador(s).Importar(Path.Combine(viejo, "backups.db"), "tester", null, CancellationToken.None);
            Afirmar(r.Respaldos == 2, "2 respaldos importados, hubo " + r.Respaldos + " " + string.Join(";", r.Avisos));
            Afirmar(r.Objetivos == 1, "los 2 objetivos repetidos de Python se juntan en 1");
            Afirmar(r.Usuarios == 1, "se importa oper; el admin ya existia en el deposito nuevo y no se pisa");
            Afirmar(r.Avisos.Count == 0, "sin avisos: " + string.Join(";", r.Avisos));

            ResultadoLogin login = s.Usuarios.Entrar("oper", "clave123");
            Afirmar(login.Usuario != null, "la contrasena de Python sigue sirviendo");
            Afirmar(s.Usuarios.Listar().First(u => u.Username == "oper").PasswordHash.StartsWith("pbkdf2$"), "y se migro a formato seguro");

            SnapshotManifest primero = s.Repo.LeerManifiestos().Manifiestos.Last();
            Afirmar(primero.Note.StartsWith("primero"), "orden y nota conservados");
            s.Restaurar(primero.Id, c.Destino, null, false, "tester", null, CancellationToken.None);
            Afirmar(File.ReadAllText(Path.Combine(c.Destino, "receta.xml")) == "<receta>1</receta>", "se restaura la version vieja exacta");
            Afirmar(s.RevisarIntegridad(null, false, 0, "tester", null, CancellationToken.None).TodoBien, "todo lo importado esta sano");
            ResultadoImportacion otra = new Importador(s).Importar(Path.Combine(viejo, "backups.db"), "tester", null, CancellationToken.None);
            Afirmar(otra.Respaldos == 0, "importar dos veces no duplica");
            Afirmar(s.Auditoria.VerificarCadena().Integra, "auditoria integra tras importar");
        }

        private static void ProgramadoPrueba(Carpeta c)
        {
            c.Escribir("a.txt", "a");
            RebithService s = Servicio(c);
            TargetInfo t = Objetivo(s, c);
            t.CreatedLocal = Tiempo.Iso(DateTime.Now.AddDays(-2));
            t.Schedule = new ScheduleInfo { Frequency = Frecuencias.Diario, Hour = 0, Minute = 0 };
            s.Repo.GuardarObjetivo(t);
            ResultadoProgramado r = s.EjecutarPendientes(false, CancellationToken.None);
            Afirmar(s.Repo.RespaldosDe(t.Id).Count == 1, "corrio el pendiente: " + string.Join(" | ", r.Lineas));
            ResultadoProgramado r2 = s.EjecutarPendientes(false, CancellationToken.None);
            Afirmar(s.Repo.RespaldosDe(t.Id).Count == 1, "no repite en el mismo turno");

            var otro = new Carpeta();
            TargetInfo roto = s.Repo.GuardarObjetivo(new TargetInfo { Name = "Roto", SourcePath = otro.Origen, IsFolder = true, Schedule = new ScheduleInfo { Frequency = Frecuencias.Diario, Hour = 0, Minute = 0 } });
            roto.CreatedLocal = Tiempo.Iso(DateTime.Now.AddDays(-2));
            s.Repo.GuardarObjetivo(roto);
            otro.Borrar();
            ResultadoProgramado r3 = s.EjecutarPendientes(false, CancellationToken.None);
            Afirmar(r3.HuboErrores, "reporta el error");
            TargetInfo guardado = s.Repo.BuscarObjetivo(roto.Id);
            Afirmar(guardado.Schedule.LastResult.StartsWith("ERROR") && guardado.Schedule.RetryAfterLocal.Length > 0, "anota el error y el reintento");
            Afirmar(s.Repo.LeerAjustes().LastAutoVerifyLocal.Length > 0, "corrio la revision automatica de integridad");
        }
    }
}
