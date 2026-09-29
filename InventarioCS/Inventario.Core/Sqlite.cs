using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Inventario.Core
{
    public class SqliteException : Exception
    {
        public int Codigo { get; private set; }

        public SqliteException(int codigo, string mensaje) : base(mensaje)
        {
            Codigo = codigo;
        }

        /// <summary>Se violo un UNIQUE (nombre de rack repetido, Lot ID repetido en el rack).</summary>
        public bool EsRestriccion { get { return (Codigo & 0xFF) == 19; } }
    }

    /// <summary>
    /// Conexion minima a SQLite. En Windows usa winsqlite3.dll, que ya viene en
    /// Windows 10/11 (System32), asi que no hay que copiar ninguna DLL nativa junto al .exe.
    /// Las pruebas compilan con SQLITE_SISTEMA para usar la libsqlite3 del sistema (macOS/Linux).
    /// Lee y escribe la misma retrabajo_pcb.db que la version en Python.
    /// </summary>
    public sealed class Sqlite : IDisposable
    {
#if SQLITE_SISTEMA
        private const string Lib = "sqlite3";
        private const CallingConvention Conv = CallingConvention.Cdecl;
#else
        private const string Lib = "winsqlite3";
        private const CallingConvention Conv = CallingConvention.StdCall;
#endif
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_INTEGER = 1;
        private const int SQLITE_FLOAT = 2;
        private const int SQLITE_NULL = 5;
        private const int OPEN_READWRITE = 0x02;
        private const int OPEN_CREATE = 0x04;
        private static readonly IntPtr Transitorio = new IntPtr(-1);

        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_open_v2(byte[] archivo, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(Lib, CallingConvention = Conv)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_extended_errcode(IntPtr db);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nBytes, out IntPtr stmt, IntPtr resto);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_bind_int64(IntPtr stmt, int i, long valor);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_bind_double(IntPtr stmt, int i, double valor);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_bind_text(IntPtr stmt, int i, byte[] valor, int nBytes, IntPtr destructor);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_bind_null(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_column_type(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = Conv)] private static extern long sqlite3_column_int64(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = Conv)] private static extern double sqlite3_column_double(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = Conv)] private static extern IntPtr sqlite3_column_text(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_column_bytes(IntPtr stmt, int i);
        [DllImport(Lib, CallingConvention = Conv)] private static extern long sqlite3_last_insert_rowid(IntPtr db);
        [DllImport(Lib, CallingConvention = Conv)] private static extern int sqlite3_changes(IntPtr db);

        private IntPtr db;

        public Sqlite(string ruta)
        {
            int rc = sqlite3_open_v2(Utf8(ruta), out db, OPEN_READWRITE | OPEN_CREATE, IntPtr.Zero);
            if (rc != SQLITE_OK)
            {
                string msg = db != IntPtr.Zero ? Texto(sqlite3_errmsg(db), -1) : "codigo " + rc;
                if (db != IntPtr.Zero) sqlite3_close_v2(db);
                db = IntPtr.Zero;
                throw new SqliteException(rc, "No se pudo abrir la base " + ruta + ": " + msg);
            }
            // Si la version en Python esta escribiendo al mismo tiempo, espera en vez de fallar.
            sqlite3_busy_timeout(db, 5000);
        }

        /// <summary>INSERT/UPDATE/DELETE/CREATE. Regresa cuantas filas cambiaron.</summary>
        public int Ejecutar(string sql, params object[] parametros)
        {
            IntPtr stmt = Preparar(sql, parametros);
            try
            {
                int rc = sqlite3_step(stmt);
                if (rc != SQLITE_DONE && rc != SQLITE_ROW) throw Error();
                return sqlite3_changes(db);
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }

        /// <summary>SELECT. Cada fila es un arreglo de valores (long, double, string o null).</summary>
        public List<object[]> Consultar(string sql, params object[] parametros)
        {
            var filas = new List<object[]>();
            IntPtr stmt = Preparar(sql, parametros);
            try
            {
                int columnas = sqlite3_column_count(stmt);
                while (true)
                {
                    int rc = sqlite3_step(stmt);
                    if (rc == SQLITE_DONE) break;
                    if (rc != SQLITE_ROW) throw Error();
                    var fila = new object[columnas];
                    for (int i = 0; i < columnas; i++)
                    {
                        switch (sqlite3_column_type(stmt, i))
                        {
                            case SQLITE_NULL: fila[i] = null; break;
                            case SQLITE_INTEGER: fila[i] = sqlite3_column_int64(stmt, i); break;
                            case SQLITE_FLOAT: fila[i] = sqlite3_column_double(stmt, i); break;
                            default:
                                IntPtr p = sqlite3_column_text(stmt, i);
                                fila[i] = Texto(p, sqlite3_column_bytes(stmt, i));
                                break;
                        }
                    }
                    filas.Add(fila);
                }
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
            return filas;
        }

        public object Escalar(string sql, params object[] parametros)
        {
            List<object[]> filas = Consultar(sql, parametros);
            return filas.Count > 0 && filas[0].Length > 0 ? filas[0][0] : null;
        }

        public long UltimoId { get { return sqlite3_last_insert_rowid(db); } }

        private IntPtr Preparar(string sql, object[] parametros)
        {
            if (db == IntPtr.Zero) throw new ObjectDisposedException("Sqlite");
            IntPtr stmt;
            if (sqlite3_prepare_v2(db, Utf8(sql), -1, out stmt, IntPtr.Zero) != SQLITE_OK) throw Error();
            for (int i = 0; i < parametros.Length; i++)
            {
                object v = parametros[i];
                int rc;
                if (v == null) rc = sqlite3_bind_null(stmt, i + 1);
                else if (v is string)
                {
                    byte[] b = Encoding.UTF8.GetBytes((string)v);
                    rc = sqlite3_bind_text(stmt, i + 1, b, b.Length, Transitorio);
                }
                else if (v is int || v is long) rc = sqlite3_bind_int64(stmt, i + 1, Convert.ToInt64(v));
                else if (v is double) rc = sqlite3_bind_double(stmt, i + 1, (double)v);
                else
                {
                    sqlite3_finalize(stmt);
                    throw new ArgumentException("Tipo de parametro no soportado: " + v.GetType().Name);
                }
                if (rc != SQLITE_OK)
                {
                    SqliteException ex = Error();
                    sqlite3_finalize(stmt);
                    throw ex;
                }
            }
            return stmt;
        }

        private SqliteException Error()
        {
            return new SqliteException(sqlite3_extended_errcode(db), Texto(sqlite3_errmsg(db), -1));
        }

        private static byte[] Utf8(string s)
        {
            // Terminado en \0, como lo espera la API en C.
            return Encoding.UTF8.GetBytes(s + "\0");
        }

        private static string Texto(IntPtr p, int bytes)
        {
            if (p == IntPtr.Zero) return null;
            if (bytes < 0)
            {
                bytes = 0;
                while (Marshal.ReadByte(p, bytes) != 0) bytes++;
            }
            var b = new byte[bytes];
            Marshal.Copy(p, b, 0, bytes);
            return Encoding.UTF8.GetString(b);
        }

        public void Dispose()
        {
            if (db != IntPtr.Zero)
            {
                sqlite3_close_v2(db);
                db = IntPtr.Zero;
            }
        }
    }
}
