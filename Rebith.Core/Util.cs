using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Rebith.Core
{
    public static class Tiempo
    {
        public const string FormatoIso = "yyyy-MM-ddTHH:mm:ss";

        public static string AhoraIso()
        {
            return DateTime.Now.ToString(FormatoIso, CultureInfo.InvariantCulture);
        }

        public static string Iso(DateTime fecha)
        {
            return fecha.ToString(FormatoIso, CultureInfo.InvariantCulture);
        }

        /// <summary>Convierte texto ISO a fecha. Texto vacio o invalido = null.</summary>
        public static DateTime? Leer(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return null;
            DateTime fecha;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out fecha))
                return fecha;
            return null;
        }

        /// <summary>Fecha legible para la pantalla: 18/09/2026 14:05.</summary>
        public static string Legible(string iso)
        {
            DateTime? fecha = Leer(iso);
            return fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : "-";
        }
    }

    public static class Hashes
    {
        public static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static string Sha256Texto(string texto)
        {
            using (var sha = SHA256.Create())
            {
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(texto ?? "")));
            }
        }

        public static bool EsHashValido(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char c in hash)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }
    }

    public static class Tamanos
    {
        public static string Legible(long bytes)
        {
            string[] unidades = { "B", "KB", "MB", "GB", "TB" };
            double valor = bytes;
            int i = 0;
            while (valor >= 1024 && i < unidades.Length - 1)
            {
                valor /= 1024;
                i++;
            }
            return valor.ToString(i == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + unidades[i];
        }
    }

    public static class Rutas
    {
        /// <summary>Ruta relativa con "/" como separador (formato del manifiesto).</summary>
        public static string Relativa(string raiz, string completa)
        {
            string r = Path.GetFullPath(raiz).TrimEnd('\\', '/');
            string c = Path.GetFullPath(completa);
            if (!c.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("La ruta no esta dentro de la carpeta: " + completa);
            return c.Substring(r.Length).TrimStart('\\', '/').Replace('\\', '/');
        }

        /// <summary>
        /// Une destino + ruta relativa del manifiesto y garantiza que el resultado quede
        /// DENTRO del destino (bloquea "..", rutas absolutas y unidades).
        /// </summary>
        public static string UnirSegura(string destino, string relativa)
        {
            if (string.IsNullOrWhiteSpace(relativa))
                throw new RebithException("Ruta vacia en el respaldo.");
            string limpia = relativa.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(limpia) || limpia.Contains(":"))
                throw new RebithException("Ruta no permitida en el respaldo: " + relativa);
            foreach (string parte in limpia.Split(Path.DirectorySeparatorChar))
            {
                if (parte == "..") throw new RebithException("Ruta no permitida en el respaldo: " + relativa);
            }
            string raiz = Path.GetFullPath(destino).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string completa = Path.GetFullPath(Path.Combine(raiz, limpia));
            if (!completa.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
                throw new RebithException("Ruta no permitida en el respaldo: " + relativa);
            return completa;
        }

        public static bool MismaUnidad(string a, string b)
        {
            try
            {
                string ra = Path.GetPathRoot(Path.GetFullPath(a));
                string rb = Path.GetPathRoot(Path.GetFullPath(b));
                return string.Equals(ra, rb, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>true si una carpeta esta dentro de la otra (o son la misma).</summary>
        public static bool SeTraslapan(string a, string b)
        {
            try
            {
                string fa = Path.GetFullPath(a).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                string fb = Path.GetFullPath(b).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                return fa.StartsWith(fb, StringComparison.OrdinalIgnoreCase) || fb.StartsWith(fa, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Espacio libre del disco de la ruta. -1 si no se puede saber (por ejemplo, red).</summary>
        public static long EspacioLibre(string ruta)
        {
            try
            {
                string raiz = Path.GetPathRoot(Path.GetFullPath(ruta));
                if (string.IsNullOrEmpty(raiz) || raiz.StartsWith("\\\\")) return -1;
                return new DriveInfo(raiz).AvailableFreeSpace;
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// Patrones de exclusion con la misma regla que fnmatch de Python:
    /// * = cualquier texto (incluye "/"), ? = un caracter, [abc] = uno de la lista.
    /// Sin distinguir mayusculas. Se prueba contra la ruta relativa y contra el nombre.
    /// </summary>
    public class Exclusiones
    {
        private readonly List<Regex> patrones = new List<Regex>();

        public Exclusiones(IEnumerable<string> textos)
        {
            if (textos == null) return;
            foreach (string texto in textos)
            {
                if (string.IsNullOrWhiteSpace(texto)) continue;
                patrones.Add(new Regex(AExpresion(texto.Trim().Replace('\\', '/')),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            }
        }

        public bool Vacia { get { return patrones.Count == 0; } }

        public bool Excluye(string rutaRelativa)
        {
            if (patrones.Count == 0) return false;
            string ruta = rutaRelativa.Replace('\\', '/');
            int barra = ruta.LastIndexOf('/');
            string nombre = barra >= 0 ? ruta.Substring(barra + 1) : ruta;
            foreach (Regex r in patrones)
            {
                if (r.IsMatch(ruta) || r.IsMatch(nombre)) return true;
            }
            return false;
        }

        public static List<string> DesdeTexto(string texto)
        {
            var lista = new List<string>();
            if (string.IsNullOrWhiteSpace(texto)) return lista;
            foreach (string parte in texto.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = parte.Trim();
                if (p.Length > 0 && !lista.Contains(p)) lista.Add(p);
            }
            return lista;
        }

        private static string AExpresion(string patron)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < patron.Length; i++)
            {
                char c = patron[i];
                if (c == '*') sb.Append(".*");
                else if (c == '?') sb.Append('.');
                else if (c == '[')
                {
                    int cierre = patron.IndexOf(']', i + 1);
                    if (cierre < 0)
                    {
                        sb.Append("\\[");
                    }
                    else
                    {
                        string dentro = patron.Substring(i + 1, cierre - i - 1);
                        if (dentro.StartsWith("!")) dentro = "^" + dentro.Substring(1);
                        sb.Append('[').Append(dentro.Replace("\\", "\\\\")).Append(']');
                        i = cierre;
                    }
                }
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return sb.ToString();
        }
    }

    public class ArchivoEncontrado
    {
        public string RutaCompleta;
        public string RutaRelativa;
    }

    /// <summary>
    /// Recorre una carpeta sin romperse: una subcarpeta sin permiso o que desaparece
    /// se anota y se sigue con las demas. No entra a enlaces (junctions) para no
    /// caer en ciclos ni salirse de la carpeta.
    /// </summary>
    public static class Recorrido
    {
        public static List<ArchivoEncontrado> Archivos(string raiz, Exclusiones exclusiones,
            List<SkippedEntry> omitidos, Func<bool> cancelado)
        {
            var resultado = new List<ArchivoEncontrado>();
            var pendientes = new Stack<string>();
            pendientes.Push(Path.GetFullPath(raiz));
            string raizCompleta = Path.GetFullPath(raiz);

            while (pendientes.Count > 0)
            {
                if (cancelado != null && cancelado()) throw new OperationCanceledException();
                string carpeta = pendientes.Pop();
                string relCarpeta = carpeta.Length > raizCompleta.Length ? Rutas.Relativa(raizCompleta, carpeta) : "";

                FileSystemInfo[] entradas;
                try
                {
                    entradas = new DirectoryInfo(carpeta).GetFileSystemInfos();
                }
                catch (Exception ex)
                {
                    if (omitidos != null)
                        omitidos.Add(new SkippedEntry { Path = relCarpeta.Length == 0 ? "(carpeta raiz)" : relCarpeta + "/", Reason = "No se pudo leer la carpeta: " + ex.Message });
                    continue;
                }

                foreach (FileSystemInfo entrada in entradas.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                {
                    string rel = relCarpeta.Length == 0 ? entrada.Name : relCarpeta + "/" + entrada.Name;
                    bool esCarpeta = (entrada.Attributes & FileAttributes.Directory) != 0;
                    if (esCarpeta)
                    {
                        if (exclusiones != null && exclusiones.Excluye(rel)) continue;
                        if ((entrada.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            if (omitidos != null)
                                omitidos.Add(new SkippedEntry { Path = rel + "/", Reason = "Es un enlace (junction/symlink); no se sigue." });
                            continue;
                        }
                        pendientes.Push(entrada.FullName);
                    }
                    else
                    {
                        if (exclusiones != null && exclusiones.Excluye(rel)) continue;
                        resultado.Add(new ArchivoEncontrado { RutaCompleta = entrada.FullName, RutaRelativa = rel });
                    }
                }
            }
            resultado.Sort((a, b) => string.Compare(a.RutaRelativa, b.RutaRelativa, StringComparison.OrdinalIgnoreCase));
            return resultado;
        }
    }
}
