using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Rebith.Core
{
    /// <summary>
    /// Lector de SOLO LECTURA del formato de archivo SQLite 3, suficiente para leer las
    /// tablas de backups.db de la version en Python. No usa DLLs nativas (System.Data.SQLite
    /// necesita SQLite.Interop.dll de 32 o 64 bits, que es una fuente comun de fallas).
    /// Soporta paginas interiores y hojas de tablas, desbordamiento (overflow) y UTF-8.
    /// Nunca escribe en el archivo.
    /// </summary>
    public class SqliteLector
    {
        private readonly byte[] datos;
        private readonly int tamanoPagina;
        private readonly int reservado;

        public SqliteLector(string ruta)
        {
            if (File.Exists(ruta + "-wal") && new FileInfo(ruta + "-wal").Length > 0)
                throw new RebithException("La base " + ruta + " tiene cambios sin consolidar (archivo -wal). Abra y cierre la version anterior una vez, o copie tambien el -wal.");
            datos = File.ReadAllBytes(ruta);
            if (datos.Length < 100 || Encoding.ASCII.GetString(datos, 0, 15) != "SQLite format 3")
                throw new RebithException("El archivo no es una base de datos SQLite: " + ruta);
            int p = (datos[16] << 8) | datos[17];
            tamanoPagina = p == 1 ? 65536 : p;
            reservado = datos[20];
            int codificacion = (datos[56] << 24) | (datos[57] << 16) | (datos[58] << 8) | datos[59];
            if (codificacion != 0 && codificacion != 1)
                throw new RebithException("La base no esta en UTF-8.");
        }

        /// <summary>Filas de una tabla, cada una como diccionario columna -> valor (long, double, string, byte[] o null).</summary>
        public List<Dictionary<string, object>> LeerTabla(string tabla)
        {
            string sql;
            int raiz = BuscarTabla(tabla, out sql);
            if (raiz <= 0) return new List<Dictionary<string, object>>();
            List<string> columnas = ColumnasDe(sql);
            var filas = new List<Dictionary<string, object>>();
            foreach (var registro in RecorrerTabla(raiz))
            {
                var fila = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < columnas.Count; i++)
                {
                    object valor = i < registro.Valores.Count ? registro.Valores[i] : null;
                    // Una columna INTEGER PRIMARY KEY se guarda como null: su valor es el rowid.
                    if (valor == null && i == IndicePk(sql, columnas)) valor = registro.RowId;
                    fila[columnas[i]] = valor;
                }
                filas.Add(fila);
            }
            return filas;
        }

        public bool ExisteTabla(string tabla)
        {
            string sql;
            return BuscarTabla(tabla, out sql) > 0;
        }

        private class Registro
        {
            public long RowId;
            public List<object> Valores;
        }

        private int BuscarTabla(string tabla, out string sql)
        {
            sql = null;
            foreach (Registro r in RecorrerTabla(1))
            {
                // sqlite_master: type, name, tbl_name, rootpage, sql
                if (r.Valores.Count < 5) continue;
                if (!"table".Equals(r.Valores[0] as string)) continue;
                if (!string.Equals(r.Valores[1] as string, tabla, StringComparison.OrdinalIgnoreCase)) continue;
                sql = r.Valores[4] as string ?? "";
                return Convert.ToInt32(r.Valores[3]);
            }
            return 0;
        }

        private static List<string> ColumnasDe(string sql)
        {
            int a = sql.IndexOf('(');
            int b = sql.LastIndexOf(')');
            var columnas = new List<string>();
            if (a < 0 || b <= a) return columnas;
            string cuerpo = sql.Substring(a + 1, b - a - 1);
            int nivel = 0;
            var actual = new StringBuilder();
            var partes = new List<string>();
            foreach (char c in cuerpo)
            {
                if (c == '(') nivel++;
                if (c == ')') nivel--;
                if (c == ',' && nivel == 0)
                {
                    partes.Add(actual.ToString());
                    actual.Clear();
                }
                else actual.Append(c);
            }
            partes.Add(actual.ToString());
            foreach (string parte in partes)
            {
                string p = parte.Trim();
                if (p.Length == 0) continue;
                string primera = p.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                string mayus = primera.ToUpperInvariant();
                if (mayus == "FOREIGN" || mayus == "PRIMARY" || mayus == "UNIQUE" || mayus == "CHECK" || mayus == "CONSTRAINT") continue;
                columnas.Add(primera.Trim('"', '`', '[', ']'));
            }
            return columnas;
        }

        private static int IndicePk(string sql, List<string> columnas)
        {
            int a = sql.IndexOf('(');
            string cuerpo = a >= 0 ? sql.Substring(a + 1) : sql;
            foreach (string parte in cuerpo.Split(','))
            {
                string p = parte.Trim();
                if (p.ToUpperInvariant().Contains("INTEGER PRIMARY KEY"))
                {
                    string nombre = p.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim('"', '`', '[', ']');
                    return columnas.FindIndex(c => string.Equals(c, nombre, StringComparison.OrdinalIgnoreCase));
                }
            }
            return -1;
        }

        private IEnumerable<Registro> RecorrerTabla(int raiz)
        {
            var pendientes = new Stack<int>();
            pendientes.Push(raiz);
            var vistas = new HashSet<int>();
            while (pendientes.Count > 0)
            {
                int pagina = pendientes.Pop();
                if (!vistas.Add(pagina)) throw new RebithException("La base de datos esta danada (paginas en ciclo).");
                int inicio = (pagina - 1) * tamanoPagina;
                int encabezado = pagina == 1 ? 100 : 0;
                int h = inicio + encabezado;
                if (h + 8 > datos.Length) throw new RebithException("La base de datos esta incompleta.");
                byte tipo = datos[h];
                int celdas = (datos[h + 3] << 8) | datos[h + 4];
                if (tipo == 0x05)
                {
                    int derecha = LeerEntero32(h + 8);
                    var hijos = new List<int>();
                    for (int i = 0; i < celdas; i++)
                    {
                        int celda = inicio + ((datos[h + 12 + i * 2] << 8) | datos[h + 13 + i * 2]);
                        hijos.Add(LeerEntero32(celda));
                    }
                    hijos.Add(derecha);
                    // Pila: se meten al reves para leer en orden.
                    for (int i = hijos.Count - 1; i >= 0; i--) pendientes.Push(hijos[i]);
                }
                else if (tipo == 0x0D)
                {
                    for (int i = 0; i < celdas; i++)
                    {
                        int celda = inicio + ((datos[h + 8 + i * 2] << 8) | datos[h + 9 + i * 2]);
                        yield return LeerCeldaHoja(celda);
                    }
                }
                else
                {
                    throw new RebithException("Tipo de pagina inesperado en la base de datos: " + tipo);
                }
            }
        }

        private Registro LeerCeldaHoja(int pos)
        {
            int n;
            long tamanoCarga = LeerVarint(pos, out n);
            pos += n;
            long rowid = LeerVarint(pos, out n);
            pos += n;
            byte[] carga = LeerCarga(pos, tamanoCarga);
            return new Registro { RowId = rowid, Valores = DecodificarRegistro(carga) };
        }

        private byte[] LeerCarga(int pos, long tamano)
        {
            int u = tamanoPagina - reservado;
            int x = u - 35;
            var carga = new byte[tamano];
            if (tamano <= x)
            {
                Buffer.BlockCopy(datos, pos, carga, 0, (int)tamano);
                return carga;
            }
            int m = ((u - 12) * 32 / 255) - 23;
            int k = m + (int)((tamano - m) % (u - 4));
            int local = k <= x ? k : m;
            Buffer.BlockCopy(datos, pos, carga, 0, local);
            int copiado = local;
            int siguiente = LeerEntero32(pos + local);
            var vistas = new HashSet<int>();
            while (copiado < tamano && siguiente != 0)
            {
                if (!vistas.Add(siguiente)) throw new RebithException("La base de datos esta danada (desbordamiento en ciclo).");
                int inicio = (siguiente - 1) * tamanoPagina;
                siguiente = LeerEntero32(inicio);
                int cuanto = (int)Math.Min(u - 4, tamano - copiado);
                Buffer.BlockCopy(datos, inicio + 4, carga, copiado, cuanto);
                copiado += cuanto;
            }
            if (copiado < tamano) throw new RebithException("La base de datos esta incompleta (registro cortado).");
            return carga;
        }

        private static List<object> DecodificarRegistro(byte[] c)
        {
            int n;
            long largoEncabezado = LeerVarint(c, 0, out n);
            int pos = n;
            var tipos = new List<long>();
            while (pos < largoEncabezado)
            {
                tipos.Add(LeerVarint(c, pos, out n));
                pos += n;
            }
            var valores = new List<object>();
            int d = (int)largoEncabezado;
            foreach (long t in tipos)
            {
                switch (t)
                {
                    case 0: valores.Add(null); break;
                    case 1: valores.Add((long)(sbyte)c[d]); d += 1; break;
                    case 2: valores.Add((long)(short)((c[d] << 8) | c[d + 1])); d += 2; break;
                    case 3: valores.Add(Entero(c, d, 3)); d += 3; break;
                    case 4: valores.Add(Entero(c, d, 4)); d += 4; break;
                    case 5: valores.Add(Entero(c, d, 6)); d += 6; break;
                    case 6: valores.Add(Entero(c, d, 8)); d += 8; break;
                    case 7:
                        {
                            long bits = Entero(c, d, 8);
                            valores.Add(BitConverter.Int64BitsToDouble(bits));
                            d += 8;
                            break;
                        }
                    case 8: valores.Add(0L); break;
                    case 9: valores.Add(1L); break;
                    default:
                        if (t >= 12 && t % 2 == 0)
                        {
                            int largo = (int)((t - 12) / 2);
                            var blob = new byte[largo];
                            Buffer.BlockCopy(c, d, blob, 0, largo);
                            valores.Add(blob);
                            d += largo;
                        }
                        else if (t >= 13)
                        {
                            int largo = (int)((t - 13) / 2);
                            valores.Add(Encoding.UTF8.GetString(c, d, largo));
                            d += largo;
                        }
                        else throw new RebithException("Tipo de dato desconocido en la base: " + t);
                        break;
                }
            }
            return valores;
        }

        private static long Entero(byte[] c, int pos, int bytes)
        {
            long v = (sbyte)c[pos];
            for (int i = 1; i < bytes; i++) v = (v << 8) | c[pos + i];
            return v;
        }

        private int LeerEntero32(int pos)
        {
            return (datos[pos] << 24) | (datos[pos + 1] << 16) | (datos[pos + 2] << 8) | datos[pos + 3];
        }

        private long LeerVarint(int pos, out int largo)
        {
            return LeerVarint(datos, pos, out largo);
        }

        private static long LeerVarint(byte[] b, int pos, out int largo)
        {
            long v = 0;
            for (int i = 0; i < 8; i++)
            {
                byte x = b[pos + i];
                v = (v << 7) | (long)(x & 0x7F);
                if ((x & 0x80) == 0)
                {
                    largo = i + 1;
                    return v;
                }
            }
            v = (v << 8) | b[pos + 8];
            largo = 9;
            return v;
        }
    }
}
