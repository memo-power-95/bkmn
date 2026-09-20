using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Rebith.Core
{
    public static class Contrasenas
    {
        private const int Iteraciones = 100000;

        /// <summary>PBKDF2-SHA256 con sal aleatoria: "pbkdf2$100000$sal$huella".</summary>
        public static string Crear(string contrasena)
        {
            byte[] sal = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(sal);
            byte[] huella = Derivar(contrasena, sal, Iteraciones);
            return "pbkdf2$" + Iteraciones + "$" + Convert.ToBase64String(sal) + "$" + Convert.ToBase64String(huella);
        }

        /// <summary>
        /// Acepta el formato nuevo y el de la version en Python ("sha256$hex", que era
        /// SHA-256 de la contrasena sin espacios al inicio o final).
        /// </summary>
        public static bool Verificar(string contrasena, string guardada, out bool esFormatoViejo)
        {
            esFormatoViejo = false;
            if (string.IsNullOrEmpty(guardada) || contrasena == null) return false;
            string[] p = guardada.Split('$');
            if (p.Length == 4 && p[0] == "pbkdf2")
            {
                int iteraciones;
                if (!int.TryParse(p[1], out iteraciones) || iteraciones < 1000) return false;
                byte[] sal, esperada;
                try
                {
                    sal = Convert.FromBase64String(p[2]);
                    esperada = Convert.FromBase64String(p[3]);
                }
                catch (FormatException)
                {
                    return false;
                }
                return IgualesEnTiempoConstante(Derivar(contrasena, sal, iteraciones), esperada);
            }
            if (p.Length == 2 && p[0] == "sha256")
            {
                esFormatoViejo = true;
                using (var sha = SHA256.Create())
                {
                    string calculada = Hashes.Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(contrasena.Trim())));
                    return IgualesEnTiempoConstante(Encoding.ASCII.GetBytes(calculada), Encoding.ASCII.GetBytes(p[1].ToLowerInvariant()));
                }
            }
            return false;
        }

        private static byte[] Derivar(string contrasena, byte[] sal, int iteraciones)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(contrasena, sal, iteraciones, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32);
            }
        }

        private static bool IgualesEnTiempoConstante(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diferencia = 0;
            for (int i = 0; i < a.Length; i++) diferencia |= a[i] ^ b[i];
            return diferencia == 0;
        }

        public static string ValidarNueva(string contrasena)
        {
            if (string.IsNullOrEmpty(contrasena) || contrasena.Length < 6) return "La contrasena debe tener al menos 6 caracteres.";
            if (contrasena.Trim() != contrasena) return "La contrasena no puede empezar ni terminar con espacios.";
            return null;
        }
    }

    public enum Permiso
    {
        Respaldar,
        Restaurar,
        Borrar,
        GestionarUsuarios,
        GestionarSeguridad
    }

    public class ResultadoLogin
    {
        public UserInfo Usuario;
        public string Error;
        public bool DebeCambiarContrasena;
    }

    /// <summary>
    /// Usuarios guardados en config\usuarios.json del deposito (asi valen en cualquier PC
    /// que use ese deposito). Bloqueo de 5 minutos tras 5 intentos fallidos. Nunca deja
    /// el sistema sin un administrador activo.
    /// </summary>
    public class UserStore
    {
        public const int IntentosMaximos = 5;
        public const int MinutosBloqueo = 5;
        public const string AdminInicial = "admin";
        public const string ContrasenaInicial = "admin123";

        private readonly string ruta;
        private readonly object candado = new object();
        private readonly Auditoria auditoria;

        public UserStore(string carpetaConfig, Auditoria auditoria)
        {
            ruta = Path.Combine(carpetaConfig, "usuarios.json");
            this.auditoria = auditoria;
            AsegurarAdministrador();
        }

        public static bool Tiene(UserInfo u, Permiso permiso)
        {
            if (u == null || !u.IsActive) return false;
            if (u.EsAdmin) return true;
            switch (permiso)
            {
                case Permiso.Respaldar: return u.CanBackup;
                case Permiso.Restaurar: return u.CanRestore;
                case Permiso.Borrar: return u.CanDelete;
                case Permiso.GestionarUsuarios: return u.CanManageUsers;
                case Permiso.GestionarSeguridad: return u.CanManageSecurity;
            }
            return false;
        }

        private List<UserInfo> Leer()
        {
            UserList lista = JsonFile.Leer<UserList>(ruta);
            return lista == null ? new List<UserInfo>() : lista.Users;
        }

        private void Guardar(List<UserInfo> usuarios)
        {
            JsonFile.Escribir(ruta, new UserList { Users = usuarios }, true);
        }

        private void AsegurarAdministrador()
        {
            lock (candado)
            {
                List<UserInfo> usuarios = Leer();
                if (usuarios.Any(u => u.EsAdmin && u.IsActive)) return;
                UserInfo admin = usuarios.FirstOrDefault(u => string.Equals(u.Username, AdminInicial, StringComparison.OrdinalIgnoreCase));
                if (admin == null)
                {
                    admin = new UserInfo { Username = AdminInicial, CreatedLocal = Tiempo.AhoraIso() };
                    usuarios.Add(admin);
                }
                admin.Role = Roles.Admin;
                admin.IsActive = true;
                admin.CanBackup = admin.CanRestore = admin.CanDelete = admin.CanManageUsers = admin.CanManageSecurity = true;
                admin.PasswordHash = Contrasenas.Crear(ContrasenaInicial);
                admin.MustChangePassword = true;
                admin.FailedAttempts = 0;
                admin.LockedUntilLocal = "";
                Guardar(usuarios);
                auditoria.Registrar("sistema", "admin_inicial", "Se creo o reactivo el administrador inicial (debe cambiar la contrasena).");
            }
        }

        public List<UserInfo> Listar()
        {
            lock (candado)
            {
                return Leer().Select(u => u.Clonar()).OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        public ResultadoLogin Entrar(string usuario, string contrasena)
        {
            var r = new ResultadoLogin();
            usuario = (usuario ?? "").Trim();
            lock (candado)
            {
                List<UserInfo> usuarios = Leer();
                UserInfo u = usuarios.FirstOrDefault(x => string.Equals(x.Username, usuario, StringComparison.OrdinalIgnoreCase));
                if (u == null)
                {
                    r.Error = "Usuario o contrasena incorrectos.";
                    auditoria.Registrar(usuario, "login_fallido", "Usuario inexistente.");
                    return r;
                }
                DateTime? bloqueado = Tiempo.Leer(u.LockedUntilLocal);
                if (bloqueado.HasValue && bloqueado.Value > DateTime.Now)
                {
                    r.Error = "Usuario bloqueado por intentos fallidos hasta las " + bloqueado.Value.ToString("HH:mm") + ".";
                    auditoria.Registrar(u.Username, "login_bloqueado", "Intento durante el bloqueo.");
                    return r;
                }
                if (!u.IsActive)
                {
                    r.Error = "El usuario esta desactivado.";
                    auditoria.Registrar(u.Username, "login_fallido", "Usuario desactivado.");
                    return r;
                }
                bool viejo;
                if (!Contrasenas.Verificar(contrasena, u.PasswordHash, out viejo))
                {
                    u.FailedAttempts++;
                    string detalle = "Contrasena incorrecta (" + u.FailedAttempts + " de " + IntentosMaximos + ").";
                    if (u.FailedAttempts >= IntentosMaximos)
                    {
                        u.LockedUntilLocal = Tiempo.Iso(DateTime.Now.AddMinutes(MinutosBloqueo));
                        u.FailedAttempts = 0;
                        detalle += " Bloqueado " + MinutosBloqueo + " minutos.";
                    }
                    Guardar(usuarios);
                    auditoria.Registrar(u.Username, "login_fallido", detalle);
                    r.Error = "Usuario o contrasena incorrectos.";
                    return r;
                }
                u.FailedAttempts = 0;
                u.LockedUntilLocal = "";
                if (viejo) u.PasswordHash = Contrasenas.Crear(contrasena.Trim());
                Guardar(usuarios);
                auditoria.Registrar(u.Username, "login", viejo ? "Entrada correcta (contrasena migrada a formato seguro)." : "Entrada correcta.");
                r.Usuario = u.Clonar();
                r.DebeCambiarContrasena = u.MustChangePassword;
                return r;
            }
        }

        public void CambiarMiContrasena(string usuario, string actual, string nueva)
        {
            string problema = Contrasenas.ValidarNueva(nueva);
            if (problema != null) throw new RebithException(problema);
            lock (candado)
            {
                List<UserInfo> usuarios = Leer();
                UserInfo u = usuarios.FirstOrDefault(x => string.Equals(x.Username, usuario, StringComparison.OrdinalIgnoreCase));
                if (u == null) throw new RebithException("Usuario no encontrado.");
                bool viejo;
                if (!Contrasenas.Verificar(actual, u.PasswordHash, out viejo)) throw new RebithException("La contrasena actual no es correcta.");
                if (actual == nueva) throw new RebithException("La nueva contrasena debe ser distinta de la actual.");
                u.PasswordHash = Contrasenas.Crear(nueva);
                u.MustChangePassword = false;
                Guardar(usuarios);
                auditoria.Registrar(u.Username, "cambio_contrasena", "El usuario cambio su contrasena.");
            }
        }

        /// <summary>Crea (original == null) o edita un usuario. contrasena vacia al editar = no cambiarla.</summary>
        public void Guardar(UserInfo quien, string nombreOriginal, UserInfo datos, string contrasena)
        {
            if (!Tiene(quien, Permiso.GestionarUsuarios)) throw new RebithException("No tiene permiso para administrar usuarios.");
            datos.Username = (datos.Username ?? "").Trim();
            if (datos.Username.Length == 0) throw new RebithException("Escriba el nombre de usuario.");
            if (datos.Username.IndexOfAny(new[] { '\t', '\r', '\n', '$' }) >= 0) throw new RebithException("El nombre de usuario tiene caracteres no permitidos.");
            if (!Roles.Todos.Contains(datos.Role)) throw new RebithException("Rol invalido.");
            if (datos.EsAdmin && !quien.EsAdmin) throw new RebithException("Solo un administrador puede crear o editar administradores.");

            lock (candado)
            {
                List<UserInfo> usuarios = Leer();
                UserInfo existente = nombreOriginal == null ? null :
                    usuarios.FirstOrDefault(x => string.Equals(x.Username, nombreOriginal, StringComparison.OrdinalIgnoreCase));
                if (nombreOriginal != null && existente == null) throw new RebithException("El usuario ya no existe.");
                if (existente != null && existente.EsAdmin && !quien.EsAdmin) throw new RebithException("Solo un administrador puede editar a otro administrador.");
                if (usuarios.Any(x => x != existente && string.Equals(x.Username, datos.Username, StringComparison.OrdinalIgnoreCase)))
                    throw new RebithException("Ya existe el usuario \"" + datos.Username + "\".");

                UserInfo destino = existente ?? new UserInfo { CreatedLocal = Tiempo.AhoraIso() };
                if (existente == null || !string.IsNullOrEmpty(contrasena))
                {
                    string problema = Contrasenas.ValidarNueva(contrasena);
                    if (problema != null) throw new RebithException(problema);
                    destino.PasswordHash = Contrasenas.Crear(contrasena);
                    destino.MustChangePassword = true;
                }
                destino.Username = datos.Username;
                destino.Role = datos.Role;
                destino.IsActive = datos.IsActive;
                destino.CanBackup = datos.CanBackup;
                destino.CanRestore = datos.CanRestore;
                destino.CanDelete = datos.CanDelete;
                destino.CanManageUsers = datos.CanManageUsers;
                destino.CanManageSecurity = datos.CanManageSecurity;
                if (destino.IsActive) destino.LockedUntilLocal = "";
                if (existente == null) usuarios.Add(destino);

                if (!usuarios.Any(x => x.EsAdmin && x.IsActive))
                    throw new RebithException("Debe quedar al menos un administrador activo.");
                Guardar(usuarios);
                auditoria.Registrar(quien.Username, existente == null ? "usuario_creado" : "usuario_editado",
                    string.Format("{0} rol={1} activo={2} respaldar={3} restaurar={4} borrar={5} usuarios={6} seguridad={7}{8}",
                        destino.Username, destino.Role, destino.IsActive, destino.CanBackup, destino.CanRestore, destino.CanDelete,
                        destino.CanManageUsers, destino.CanManageSecurity, string.IsNullOrEmpty(contrasena) ? "" : " (contrasena restablecida)"));
            }
        }

        public void Eliminar(UserInfo quien, string usuario)
        {
            if (!Tiene(quien, Permiso.GestionarUsuarios)) throw new RebithException("No tiene permiso para administrar usuarios.");
            if (string.Equals(quien.Username, usuario, StringComparison.OrdinalIgnoreCase)) throw new RebithException("No puede eliminarse a si mismo.");
            lock (candado)
            {
                List<UserInfo> usuarios = Leer();
                UserInfo u = usuarios.FirstOrDefault(x => string.Equals(x.Username, usuario, StringComparison.OrdinalIgnoreCase));
                if (u == null) return;
                if (u.EsAdmin && !quien.EsAdmin) throw new RebithException("Solo un administrador puede eliminar a otro administrador.");
                usuarios.Remove(u);
                if (!usuarios.Any(x => x.EsAdmin && x.IsActive)) throw new RebithException("Debe quedar al menos un administrador activo.");
                Guardar(usuarios);
                auditoria.Registrar(quien.Username, "usuario_eliminado", u.Username);
            }
        }

        /// <summary>Relee al usuario (por si otro administrador lo desactivo o le quito permisos).</summary>
        public UserInfo Refrescar(string usuario)
        {
            lock (candado)
            {
                UserInfo u = Leer().FirstOrDefault(x => string.Equals(x.Username, usuario, StringComparison.OrdinalIgnoreCase));
                return u == null ? null : u.Clonar();
            }
        }
    }
}
