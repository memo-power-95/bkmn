using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Inventario.Core;

namespace Inventario.Tests
{
    /// <summary>
    /// Pruebas del motor sin librerias externas: dotnet run --project Inventario.Tests
    /// Cada prueba trabaja en su propia carpeta temporal y se borra al final.
    /// </summary>
    internal static class Program
    {
        private static int fallas;
        private static int pasadas;

        private static int Main()
        {
            // En Windows no hay libsqlite3: se usa la winsqlite3.dll que ya trae el sistema.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                NativeLibrary.SetDllImportResolver(typeof(Sqlite).Assembly,
                    (nombre, asm, ruta) => nombre == "sqlite3" ? NativeLibrary.Load("winsqlite3") : IntPtr.Zero);

            Correr("Nombres automaticos R1, R2... y nombre repetido", NombresDeRack);
            Correr("Limite de trays: 1 a 500", LimiteTrays);
            Correr("Escaneo, duplicados en todo el rack y deshacer", EscaneoYDuplicados);
            Correr("Historial con los mismos eventos que Python", Historial);
            Correr("Excel: formato, TOTAL QTY de todos los racks y Lot ID como texto", ExcelFormato);
            Correr("Excel: rack de 400 trays llega a la columna correcta", ExcelMuchosTrays);
            Correr("Letras de columna", LetrasColumna);
            Correr("Base creada por Python se lee, y Python lee lo escrito aqui", CompatibilidadPython);

            Console.WriteLine();
            Console.WriteLine("Pasaron {0}, fallaron {1}", pasadas, fallas);
            return fallas == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ utilidades

        private static void Correr(string nombre, Action<string> prueba)
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "inventario_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(carpeta);
            try
            {
                prueba(carpeta);
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
                try { Directory.Delete(carpeta, true); } catch { }
            }
        }

        private static void Afirmar(bool condicion, string mensaje)
        {
            if (!condicion) throw new Exception("Se esperaba: " + mensaje);
        }

        private static void Igual<T>(T esperado, T real, string que)
        {
            if (!EqualityComparer<T>.Default.Equals(esperado, real))
                throw new Exception(que + ": se esperaba <" + esperado + "> y llego <" + real + ">");
        }

        private static void Lanza<TEx>(Action a, string que) where TEx : Exception
        {
            try
            {
                a();
            }
            catch (TEx)
            {
                return;
            }
            throw new Exception("Se esperaba " + typeof(TEx).Name + ": " + que);
        }

        private static BaseInventario Nueva(string carpeta)
        {
            return new BaseInventario(Path.Combine(carpeta, "retrabajo_pcb.db"));
        }

        /// <summary>Lee la hoja del .xlsx como diccionario "B4" -> texto.</summary>
        private static Dictionary<string, string> LeerHoja(string ruta, out Dictionary<string, string> estilos)
        {
            estilos = new Dictionary<string, string>();
            var celdas = new Dictionary<string, string>();
            using (ZipArchive zip = ZipFile.OpenRead(ruta))
            {
                foreach (string parte in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/styles.xml", "xl/_rels/workbook.xml.rels" })
                    Afirmar(zip.GetEntry(parte) != null, "parte " + parte + " en el xlsx");
                XDocument doc;
                using (Stream s = zip.GetEntry("xl/worksheets/sheet1.xml").Open()) doc = XDocument.Load(s);
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (XElement c in doc.Descendants(ns + "c"))
                {
                    string r = (string)c.Attribute("r");
                    XElement v = c.Element(ns + "v");
                    XElement t = c.Descendants(ns + "t").FirstOrDefault();
                    celdas[r] = v != null ? v.Value : t != null ? t.Value : null;
                    estilos[r] = (string)c.Attribute("s") ?? "0";
                }
            }
            return celdas;
        }

        // ------------------------------------------------------------------ pruebas

        private static void NombresDeRack(string carpeta)
        {
            using (BaseInventario b = Nueva(carpeta))
            {
                Igual("R1", b.SiguienteNombreRackAutomatico(), "primer nombre");
                long id1 = b.CrearRack(b.SiguienteNombreRackAutomatico(), 6);
                Igual("R2", b.SiguienteNombreRackAutomatico(), "segundo nombre");
                b.CrearRack("Almacen A", 3);
                Igual("R3", b.SiguienteNombreRackAutomatico(), "sigue el contador aunque haya nombres manuales");
                Lanza<InventarioException>(() => b.CrearRack("R1", 4), "nombre repetido");
                Lanza<InventarioException>(() => b.CrearRack("   ", 4), "nombre vacio");

                List<Rack> racks = b.ListarRacks();
                Igual(2, racks.Count, "racks");
                Igual("Almacen A", racks[0].Nombre, "mas nuevo primero");
                Igual(id1 + " - R1 (6 trays)", racks[1].ToString(), "texto del combo");
            }
        }

        private static void LimiteTrays(string carpeta)
        {
            using (BaseInventario b = Nueva(carpeta))
            {
                Igual(500, BaseInventario.MaxTraysPorRack, "limite");
                b.CrearRack("grande", 500);
                b.CrearRack("uno", 1);
                Lanza<InventarioException>(() => b.CrearRack("demasiado", 501), "501 trays");
                Lanza<InventarioException>(() => b.CrearRack("cero", 0), "0 trays");
            }
        }

        private static void EscaneoYDuplicados(string carpeta)
        {
            using (BaseInventario b = Nueva(carpeta))
            {
                long r1 = b.CrearRack("R1", 3);
                long r2 = b.CrearRack("R2", 3);
                Igual<int?>(null, b.Escanear(r1, 1, "LOT-A"), "agrega LOT-A");
                Igual<int?>(null, b.Escanear(r1, 1, "  LOT-B  "), "agrega LOT-B (con espacios)");
                Igual<int?>(null, b.Escanear(r1, 2, "LOT-C"), "agrega LOT-C");
                Igual<int?>(1, b.Escanear(r1, 3, "LOT-A"), "LOT-A ya esta en tray 1");
                Igual<int?>(1, b.Escanear(r1, 1, "LOT-B"), "LOT-B ya esta");
                Igual<int?>(null, b.Escanear(r2, 1, "LOT-A"), "el mismo Lot ID si puede ir en otro rack");

                Dictionary<int, int> q = b.QtyPorTray(r1);
                Igual(2, q[1], "qty tray 1");
                Igual(1, q[2], "qty tray 2");
                Afirmar(!q.ContainsKey(3), "tray 3 vacio");

                Igual("LOT-B", b.DeshacerUltimo(r1, 1), "deshacer el ultimo del tray 1");
                Igual<string>(null, b.DeshacerUltimo(r1, 3), "tray vacio no deshace nada");
                CollectionIgual(new[] { "LOT-A" }, b.ItemsDeTray(r1, 1), "tray 1 tras deshacer");
                Igual<int?>(null, b.Escanear(r1, 3, "LOT-B"), "tras deshacer, LOT-B se puede volver a escanear");
            }
        }

        private static void CollectionIgual(IList<string> esperado, IList<string> real, string que)
        {
            Igual(string.Join(",", esperado), string.Join(",", real), que);
        }

        private static void Historial(string carpeta)
        {
            string ruta = Path.Combine(carpeta, "retrabajo_pcb.db");
            long r;
            using (var b = new BaseInventario(ruta))
            {
                r = b.CrearRack("R1", 2);
                b.Escanear(r, 2, "LOT-X");
                b.Escanear(r, 1, "LOT-X");   // duplicado: no se anota
                b.DeshacerUltimo(r, 2);
            }
            using (var db = new Sqlite(ruta))
            {
                List<object[]> filas = db.Consultar("SELECT evento, detalle, fecha FROM historial ORDER BY id");
                Igual(3, filas.Count, "eventos");
                Igual("rack_creado|R1 (2 trays)", filas[0][0] + "|" + filas[0][1], "evento 1");
                Igual("inventario_escaneado|rack " + r + " tray 2: LOT-X", filas[1][0] + "|" + filas[1][1], "evento 2");
                Igual("inventario_deshacer|rack " + r + " tray 2: LOT-X", filas[2][0] + "|" + filas[2][1], "evento 3");
                string fecha = (string)filas[0][2];
                Afirmar(fecha.Length == 19 && fecha[10] == 'T', "fecha como isoformat de Python: " + fecha);
            }
        }

        private static void ExcelFormato(string carpeta)
        {
            string xlsx = Path.Combine(carpeta, "inv.xlsx");
            using (BaseInventario b = Nueva(carpeta))
            {
                long r1 = b.CrearRack("R1", 2);
                long r2 = b.CrearRack("R2", 3);
                b.Escanear(r1, 1, "00123");
                b.Escanear(r1, 1, "L2");
                b.Escanear(r1, 2, "L3");
                b.Escanear(r2, 3, "L4 & <5>");
                ExcelInventario.Escribir(xlsx, b.ExportarTodos());
            }
            Dictionary<string, string> estilos;
            Dictionary<string, string> c = LeerHoja(xlsx, out estilos);
            Igual("NUMERO DE RACK", c["A1"], "A1");
            Igual("# TRAY", c["A2"], "A2");
            Igual("QTY", c["A3"], "A3");
            Igual("4", c["A4"], "TOTAL QTY = suma de TODOS los racks (antes solo el primero)");
            // ExportarTodos va del rack mas nuevo al mas viejo, como en Python
            Igual("R2", c["B1"], "B1");
            Igual("R2-1", c["B2"], "B2");
            Igual("0", c["B3"], "qty vacio");
            Igual("R2-3", c["D2"], "D2");
            Igual("L4 & <5>", c["D4"], "texto con simbolos");
            Igual("R1-1", c["E2"], "E2");
            Igual("2", c["E3"], "qty R1-1");
            Igual("00123", c["E4"], "Lot ID con ceros a la izquierda sigue como texto");
            Igual("L2", c["E5"], "orden de escaneo");
            Igual("L3", c["F4"], "F4");
            Igual("2", estilos["E3"], "QTY en verde");
            Igual("1", estilos["E2"], "encabezado en negrita");
            Afirmar(!File.Exists(xlsx + ".tmp"), "no queda el temporal");
        }

        private static void ExcelMuchosTrays(string carpeta)
        {
            string xlsx = Path.Combine(carpeta, "inv.xlsx");
            using (BaseInventario b = Nueva(carpeta))
            {
                long r = b.CrearRack("R1", 400);
                for (int i = 1; i <= 300; i++) b.Escanear(r, 400, "LOT" + i);
                ExcelInventario.Escribir(xlsx, new[] { b.ExportarRack(r) });
            }
            Dictionary<string, string> estilos;
            Dictionary<string, string> c = LeerHoja(xlsx, out estilos);
            string col = ExcelInventario.LetraColumna(401);
            Igual("OK", col, "columna 401");
            Igual("R1-400", c[col + "2"], "tray 400");
            Igual("300", c[col + "3"], "qty tray 400");
            Igual("LOT300", c[col + "303"], "ultimo Lot ID");
            Igual("300", c["A4"], "total");
        }

        private static void LetrasColumna(string carpeta)
        {
            Igual("A", ExcelInventario.LetraColumna(1), "1");
            Igual("Z", ExcelInventario.LetraColumna(26), "26");
            Igual("AA", ExcelInventario.LetraColumna(27), "27");
            Igual("AZ", ExcelInventario.LetraColumna(52), "52");
            Igual("ZZ", ExcelInventario.LetraColumna(702), "702");
            Igual("AAA", ExcelInventario.LetraColumna(703), "703");
        }

        /// <summary>
        /// Crea la base con el mismo esquema y las mismas consultas de inv.py (via python3 y
        /// su modulo sqlite3), la abre desde C#, escribe, y comprueba que Python lo lee igual.
        /// Si no hay python3 en el equipo, la prueba se omite.
        /// </summary>
        private static void CompatibilidadPython(string carpeta)
        {
            string ruta = Path.Combine(carpeta, "retrabajo_pcb.db");
            string crear = @"
import sqlite3, sys
c = sqlite3.connect(sys.argv[1])
c.execute('''CREATE TABLE IF NOT EXISTS historial (id INTEGER PRIMARY KEY AUTOINCREMENT, evento TEXT, detalle TEXT, fecha TEXT)''')
c.execute('''CREATE TABLE IF NOT EXISTS inventario_racks (id INTEGER PRIMARY KEY AUTOINCREMENT, nombre TEXT UNIQUE, num_trays INTEGER, creado TEXT)''')
c.execute('''CREATE TABLE IF NOT EXISTS inventario_items (id INTEGER PRIMARY KEY AUTOINCREMENT, rack_id INTEGER, tray_numero INTEGER, lot_id TEXT, escaneado_en TEXT, FOREIGN KEY(rack_id) REFERENCES inventario_racks(id), UNIQUE(rack_id, lot_id))''')
c.execute('''CREATE TABLE IF NOT EXISTS charolas (id INTEGER PRIMARY KEY AUTOINCREMENT, nombre TEXT)''')
c.execute(""INSERT INTO charolas (nombre) VALUES ('A1')"")
c.execute(""INSERT INTO inventario_racks (nombre, num_trays, creado) VALUES ('R1', 6, '2026-01-01T08:00:00')"")
c.execute(""INSERT INTO inventario_items (rack_id, tray_numero, lot_id, escaneado_en) VALUES (1, 2, 'PY-1', '2026-01-01T08:01:00')"")
c.commit()
";
            string leer = @"
import sqlite3, sys
c = sqlite3.connect(sys.argv[1])
cur = c.cursor()
cur.execute('SELECT id, nombre, num_trays FROM inventario_racks ORDER BY id DESC')
print(cur.fetchall())
cur.execute('SELECT tray_numero, COUNT(*) FROM inventario_items WHERE rack_id=? GROUP BY tray_numero', (1,))
print(dict(cur.fetchall()))
cur.execute('SELECT lot_id FROM inventario_items WHERE rack_id=? AND tray_numero=? ORDER BY id', (1, 2))
print([r[0] for r in cur.fetchall()])
cur.execute('SELECT nombre FROM charolas')
print(cur.fetchall())
";
            string salida;
            if (!Python(crear, ruta, out salida))
            {
                Console.WriteLine("        (sin python3: se omite)");
                return;
            }
            using (var b = new BaseInventario(ruta))
            {
                List<Rack> racks = b.ListarRacks();
                Igual(1, racks.Count, "rack creado por Python");
                Igual("R1", racks[0].Nombre, "nombre");
                Igual(6, racks[0].NumTrays, "trays");
                Igual<int?>(2, b.Escanear(1, 5, "PY-1"), "duplicado escaneado en Python se detecta");
                Igual("R2", b.SiguienteNombreRackAutomatico(), "siguiente nombre");
                Igual<int?>(null, b.Escanear(1, 2, "CS-1 ñ"), "escaneo en C# (con acento)");
                b.CrearRack("R2", 450);
            }
            Afirmar(Python(leer, ruta, out salida), "python3 lee la base: " + salida);
            string[] lineas = salida.Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Igual("[(2, 'R2', 450), (1, 'R1', 6)]", lineas[0], "racks vistos por Python");
            Igual("{2: 2}", lineas[1], "qty vista por Python");
            Igual("['PY-1', 'CS-1 ñ']", lineas[2], "Lot ID vistos por Python");
            Igual("[('A1',)]", lineas[3], "las demas tablas de Python no se tocan");
        }

        private static bool Python(string codigo, string argumento, out string salida)
        {
            salida = "";
            string script = Path.Combine(Path.GetDirectoryName(argumento), "s_" + Guid.NewGuid().ToString("N") + ".py");
            File.WriteAllText(script, codigo);
            var psi = new ProcessStartInfo("python3")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add(argumento);
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            try
            {
                using (Process p = Process.Start(psi))
                {
                    salida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
    }
}
