using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Inventario.Core
{
    public class InventarioException : Exception
    {
        public InventarioException(string mensaje) : base(mensaje)
        {
        }
    }

    public class Rack
    {
        public long Id;
        public string Nombre;
        public int NumTrays;

        /// <summary>Mismo texto que el combo de la version en Python: "3 - R3 (6 trays)".</summary>
        public override string ToString()
        {
            return Id + " - " + Nombre + " (" + NumTrays + " trays)";
        }
    }

    public class TrayExportado
    {
        public int Numero;
        public List<string> Items = new List<string>();
        public int Qty { get { return Items.Count; } }
    }

    public class RackExportado
    {
        public string Nombre;
        public List<TrayExportado> Trays = new List<TrayExportado>();
        public int TotalQty { get { return Trays.Sum(t => t.Qty); } }
    }

    /// <summary>
    /// Modulo de Inventario por Racks (Rack -> Trays -> Lot ID), portado de inv.py.
    /// Usa las mismas tablas (inventario_racks, inventario_items, historial) y los mismos
    /// eventos de historial, asi que la version en Python y esta pueden compartir la base.
    /// </summary>
    public sealed class BaseInventario : IDisposable
    {
        public const int MaxTraysPorRack = 500;   // en Python era 50

        private readonly Sqlite db;

        public string Ruta { get; private set; }

        public BaseInventario(string ruta)
        {
            Ruta = ruta;
            db = new Sqlite(ruta);
            CrearTablas();
        }

        private void CrearTablas()
        {
            db.Ejecutar(@"CREATE TABLE IF NOT EXISTS historial (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                evento TEXT,
                detalle TEXT,
                fecha TEXT
            )");
            db.Ejecutar(@"CREATE TABLE IF NOT EXISTS inventario_racks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nombre TEXT UNIQUE,
                num_trays INTEGER,
                creado TEXT
            )");
            db.Ejecutar(@"CREATE TABLE IF NOT EXISTS inventario_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                rack_id INTEGER,
                tray_numero INTEGER,
                lot_id TEXT,
                escaneado_en TEXT,
                FOREIGN KEY(rack_id) REFERENCES inventario_racks(id),
                UNIQUE(rack_id, lot_id)
            )");
        }

        /// <summary>Misma forma que datetime.now().isoformat(timespec="seconds") en Python.</summary>
        private static string Ahora()
        {
            return DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public void RegistrarHistorial(string evento, string detalle)
        {
            db.Ejecutar("INSERT INTO historial (evento, detalle, fecha) VALUES (?,?,?)", evento, detalle, Ahora());
        }

        // ------------------------------------------------------------------ racks

        /// <summary>R1, R2, R3... basado en el contador AUTOINCREMENT, igual que Python.</summary>
        public string SiguienteNombreRackAutomatico()
        {
            long ultimo = Convert.ToInt64(db.Escalar("SELECT COALESCE(MAX(id), 0) FROM inventario_racks"));
            return "R" + (ultimo + 1);
        }

        public long CrearRack(string nombre, int numTrays)
        {
            nombre = (nombre ?? "").Trim();
            if (nombre.Length == 0) throw new InventarioException("Ingresa un nombre para el rack.");
            if (numTrays < 1 || numTrays > MaxTraysPorRack)
                throw new InventarioException("El número de trays debe estar entre 1 y " + MaxTraysPorRack + ".");
            try
            {
                db.Ejecutar("INSERT INTO inventario_racks (nombre, num_trays, creado) VALUES (?,?,?)", nombre, numTrays, Ahora());
            }
            catch (SqliteException ex)
            {
                if (ex.EsRestriccion) throw new InventarioException("Ya existe un rack con ese nombre.");
                throw;
            }
            long id = db.UltimoId;
            RegistrarHistorial("rack_creado", nombre + " (" + numTrays + " trays)");
            return id;
        }

        public List<Rack> ListarRacks()
        {
            return db.Consultar("SELECT id, nombre, num_trays FROM inventario_racks ORDER BY id DESC")
                .Select(ARack).ToList();
        }

        public Rack ObtenerRack(long rackId)
        {
            List<object[]> filas = db.Consultar("SELECT id, nombre, num_trays FROM inventario_racks WHERE id=?", rackId);
            return filas.Count > 0 ? ARack(filas[0]) : null;
        }

        private static Rack ARack(object[] f)
        {
            return new Rack { Id = Convert.ToInt64(f[0]), Nombre = Convert.ToString(f[1]), NumTrays = Convert.ToInt32(f[2] ?? 0L) };
        }

        // ------------------------------------------------------------------ escaneo

        /// <summary>Un Lot ID solo puede estar UNA vez en todo el rack. Regresa su tray, o null.</summary>
        public int? TrayDeLotEnRack(long rackId, string lotId)
        {
            object t = db.Escalar("SELECT tray_numero FROM inventario_items WHERE rack_id=? AND lot_id=?", rackId, lotId);
            return t == null ? (int?)null : Convert.ToInt32(t);
        }

        /// <summary>
        /// Agrega el Lot ID al tray. Si ya estaba en el rack, no agrega nada y regresa el tray
        /// donde ya estaba; si se agrego, regresa null.
        /// </summary>
        public int? Escanear(long rackId, int trayNumero, string lotId)
        {
            lotId = (lotId ?? "").Trim();
            if (lotId.Length == 0) throw new InventarioException("El Lot ID está vacío.");
            int? existente = TrayDeLotEnRack(rackId, lotId);
            if (existente != null) return existente;
            try
            {
                db.Ejecutar("INSERT INTO inventario_items (rack_id, tray_numero, lot_id, escaneado_en) VALUES (?,?,?,?)",
                    rackId, trayNumero, lotId, Ahora());
            }
            catch (SqliteException ex)
            {
                // Otra PC (o la version en Python) lo escaneo entre la revision y el INSERT.
                if (ex.EsRestriccion) return TrayDeLotEnRack(rackId, lotId);
                throw;
            }
            RegistrarHistorial("inventario_escaneado", "rack " + rackId + " tray " + trayNumero + ": " + lotId);
            return null;
        }

        /// <summary>Quita el ultimo Lot ID escaneado en ese tray. Regresa el Lot ID, o null si estaba vacio.</summary>
        public string DeshacerUltimo(long rackId, int trayNumero)
        {
            List<object[]> filas = db.Consultar(
                "SELECT id, lot_id FROM inventario_items WHERE rack_id=? AND tray_numero=? ORDER BY id DESC LIMIT 1",
                rackId, trayNumero);
            if (filas.Count == 0) return null;
            string lotId = Convert.ToString(filas[0][1]);
            db.Ejecutar("DELETE FROM inventario_items WHERE id=?", filas[0][0]);
            RegistrarHistorial("inventario_deshacer", "rack " + rackId + " tray " + trayNumero + ": " + lotId);
            return lotId;
        }

        public Dictionary<int, int> QtyPorTray(long rackId)
        {
            return db.Consultar("SELECT tray_numero, COUNT(*) FROM inventario_items WHERE rack_id=? GROUP BY tray_numero", rackId)
                .ToDictionary(f => Convert.ToInt32(f[0]), f => Convert.ToInt32(f[1]));
        }

        public List<string> ItemsDeTray(long rackId, int trayNumero)
        {
            return db.Consultar("SELECT lot_id FROM inventario_items WHERE rack_id=? AND tray_numero=? ORDER BY id", rackId, trayNumero)
                .Select(f => Convert.ToString(f[0])).ToList();
        }

        // ------------------------------------------------------------------ exportar

        /// <summary>Por cada tray (1..num_trays), su lista de Lot ID en orden de escaneo.</summary>
        public RackExportado ExportarRack(long rackId)
        {
            Rack rack = ObtenerRack(rackId);
            if (rack == null) return null;
            var r = new RackExportado { Nombre = rack.Nombre };
            var porTray = new Dictionary<int, TrayExportado>();
            for (int t = 1; t <= rack.NumTrays; t++)
            {
                var tray = new TrayExportado { Numero = t };
                r.Trays.Add(tray);
                porTray[t] = tray;
            }
            foreach (object[] f in db.Consultar("SELECT tray_numero, lot_id FROM inventario_items WHERE rack_id=? ORDER BY id", rackId))
            {
                TrayExportado tray;
                if (porTray.TryGetValue(Convert.ToInt32(f[0]), out tray)) tray.Items.Add(Convert.ToString(f[1]));
            }
            return r;
        }

        public List<RackExportado> ExportarTodos()
        {
            return ListarRacks().Select(r => ExportarRack(r.Id)).Where(r => r != null).ToList();
        }

        public void Dispose()
        {
            db.Dispose();
        }
    }
}
