using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace Inventario.Core
{
    /// <summary>
    /// Escribe el .xlsx del inventario en el mismo formato que la version en Python:
    /// fila 1 'NUMERO DE RACK', fila 2 '# TRAY', fila 3 'QTY' (verde), y debajo los Lot ID
    /// de cada tray, un rack tras otro en columnas. A4 lleva el total de todo lo exportado.
    /// Arma el archivo a mano (es un zip con XML), sin librerias externas.
    /// </summary>
    public static class ExcelInventario
    {
        private const string NsHoja = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Indices en styles.xml
        private const int EstiloNormal = 0;
        private const int EstiloNegrita = 1;
        private const int EstiloQty = 2;

        private class Celda
        {
            public object Valor;
            public int Estilo;
        }

        public static void Escribir(string ruta, IList<RackExportado> racks)
        {
            // hoja[fila][columna], ambos desde 1
            var hoja = new SortedDictionary<int, SortedDictionary<int, Celda>>();
            System.Action<int, int, object, int> poner = (fila, columna, valor, estilo) =>
            {
                SortedDictionary<int, Celda> f;
                if (!hoja.TryGetValue(fila, out f)) hoja[fila] = f = new SortedDictionary<int, Celda>();
                f[columna] = new Celda { Valor = valor, Estilo = estilo };
            };

            poner(1, 1, "NUMERO DE RACK", EstiloNegrita);
            poner(2, 1, "# TRAY", EstiloNegrita);
            poner(3, 1, "QTY", EstiloNegrita);
            poner(4, 1, racks.Sum(r => r.TotalQty), EstiloNormal);

            int col = 2;
            foreach (RackExportado rack in racks)
            {
                foreach (TrayExportado tray in rack.Trays)
                {
                    poner(1, col, rack.Nombre, EstiloNegrita);
                    poner(2, col, rack.Nombre + "-" + tray.Numero, EstiloNegrita);
                    poner(3, col, tray.Qty, EstiloQty);
                    for (int i = 0; i < tray.Items.Count; i++) poner(4 + i, col, tray.Items[i], EstiloNormal);
                    col++;
                }
            }
            int ultimaColumna = col - 1;

            string temporal = ruta + ".tmp";
            using (var zip = new ZipArchive(File.Create(temporal), ZipArchiveMode.Create))
            {
                Texto(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");
                Texto(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                Texto(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"" + NsHoja + "\" xmlns:r=\"" + NsRel + "\">" +
                    "<sheets><sheet name=\"Inventario\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                Texto(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");
                Texto(zip, "xl/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<styleSheet xmlns=\"" + NsHoja + "\">" +
                    "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                    "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                    "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                    "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF92D050\"/><bgColor rgb=\"FF92D050\"/></patternFill></fill></fills>" +
                    "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                    "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                    "<cellXfs count=\"3\">" +
                    "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                    "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
                    "</cellXfs>" +
                    "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                    "</styleSheet>");

                ZipArchiveEntry entrada = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
                using (Stream s = entrada.Open())
                using (XmlWriter w = XmlWriter.Create(s, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
                {
                    w.WriteStartDocument(true);
                    w.WriteStartElement("worksheet", NsHoja);
                    w.WriteStartElement("cols");
                    Columna(w, 1, 1, 16);
                    if (ultimaColumna >= 2) Columna(w, 2, ultimaColumna, 18);
                    w.WriteEndElement();
                    w.WriteStartElement("sheetData");
                    foreach (var fila in hoja)
                    {
                        w.WriteStartElement("row");
                        w.WriteAttributeString("r", fila.Key.ToString(CultureInfo.InvariantCulture));
                        foreach (var c in fila.Value)
                        {
                            w.WriteStartElement("c");
                            w.WriteAttributeString("r", LetraColumna(c.Key) + fila.Key.ToString(CultureInfo.InvariantCulture));
                            if (c.Value.Estilo != EstiloNormal) w.WriteAttributeString("s", c.Value.Estilo.ToString(CultureInfo.InvariantCulture));
                            if (c.Value.Valor is int)
                            {
                                w.WriteElementString("v", ((int)c.Value.Valor).ToString(CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                // Texto siempre como texto: un Lot ID "00123" no debe volverse 123.
                                w.WriteAttributeString("t", "inlineStr");
                                w.WriteStartElement("is");
                                w.WriteStartElement("t");
                                w.WriteAttributeString("xml", "space", null, "preserve");
                                w.WriteString(Limpiar(ComoTexto(c.Value.Valor)));
                                w.WriteEndElement();
                                w.WriteEndElement();
                            }
                            w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteEndElement();
                    w.WriteEndDocument();
                }
            }
            // Se publica de un solo movimiento: nunca queda un .xlsx a medias.
            if (File.Exists(ruta)) File.Delete(ruta);
            File.Move(temporal, ruta);
        }

        private static string ComoTexto(object valor)
        {
            return System.Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
        }

        private static void Columna(XmlWriter w, int desde, int hasta, int ancho)
        {
            w.WriteStartElement("col");
            w.WriteAttributeString("min", desde.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("max", hasta.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("width", ancho.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("customWidth", "1");
            w.WriteEndElement();
        }

        /// <summary>1 -> A, 26 -> Z, 27 -> AA ...</summary>
        public static string LetraColumna(int numero)
        {
            var sb = new StringBuilder();
            while (numero > 0)
            {
                int r = (numero - 1) % 26;
                sb.Insert(0, (char)('A' + r));
                numero = (numero - 1) / 26;
            }
            return sb.ToString();
        }

        /// <summary>Quita caracteres de control que algunos escaneres mandan y que XML no admite.</summary>
        private static string Limpiar(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
                if (ch == '\t' || ch == '\n' || ch == '\r' || ch >= 0x20) sb.Append(ch);
            return sb.ToString();
        }

        private static void Texto(ZipArchive zip, string nombre, string contenido)
        {
            ZipArchiveEntry e = zip.CreateEntry(nombre, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(contenido);
        }
    }
}
