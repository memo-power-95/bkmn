using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Rebith.Core
{
    public class EventoAuditoria
    {
        public string Fecha = "";
        public string Usuario = "";
        public string Accion = "";
        public string Detalle = "";
        public string Equipo = "";
        public int Linea;
    }

    public class ResultadoCadena
    {
        public bool Integra = true;
        public int Lineas;
        public string Detalle = "";
    }

    /// <summary>
    /// Bitacora de quien hizo que. Cada linea lleva la huella de la anterior (cadena):
    /// si alguien borra o edita una linea, la verificacion lo detecta.
    /// Formato por linea (separado por tabuladores):
    ///   fecha  equipo  usuario  accion  detalle  huellaAnterior  huella
    /// Se protege con un mutex con nombre para que la ventana y el respaldo programado
    /// no escriban al mismo tiempo.
    /// </summary>
    public class Auditoria
    {
        private const string Inicio = "0000000000000000000000000000000000000000000000000000000000000000";
        private readonly string archivo;
        private readonly string nombreMutex;

        public Auditoria(string carpeta, string repoId)
        {
            Directory.CreateDirectory(carpeta);
            archivo = Path.Combine(carpeta, "auditoria.log");
            nombreMutex = "Global\\Rebith.Auditoria." + repoId;
        }

        public string Archivo { get { return archivo; } }

        /// <summary>Nunca lanza excepciones: un fallo de la bitacora no debe detener un respaldo.</summary>
        public void Registrar(string usuario, string accion, string detalle)
        {
            try
            {
                using (var mutex = new Mutex(false, nombreMutex))
                {
                    bool tomado = false;
                    try
                    {
                        try
                        {
                            tomado = mutex.WaitOne(TimeSpan.FromSeconds(10));
                        }
                        catch (AbandonedMutexException)
                        {
                            tomado = true;
                        }
                        string anterior = UltimaHuella();
                        string cuerpo = string.Join("\t", new[]
                        {
                            Tiempo.AhoraIso(), Limpiar(Environment.MachineName), Limpiar(usuario),
                            Limpiar(accion), Limpiar(detalle), anterior
                        });
                        string huella = Hashes.Sha256Texto(cuerpo);
                        byte[] bytes = Encoding.UTF8.GetBytes(cuerpo + "\t" + huella + Environment.NewLine);
                        using (var fs = new FileStream(archivo, FileMode.Append, FileAccess.Write, FileShare.Read))
                        {
                            fs.Write(bytes, 0, bytes.Length);
                            fs.Flush(true);
                        }
                    }
                    finally
                    {
                        if (tomado) mutex.ReleaseMutex();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("No se pudo escribir la auditoria (" + accion + ")", ex);
            }
        }

        private string UltimaHuella()
        {
            if (!File.Exists(archivo)) return Inicio;
            string ultima = null;
            foreach (string linea in LeerLineas())
            {
                if (linea.Trim().Length > 0) ultima = linea;
            }
            if (ultima == null) return Inicio;
            string[] partes = ultima.Split('\t');
            return partes.Length >= 7 ? partes[6] : Inicio;
        }

        private IEnumerable<string> LeerLineas()
        {
            if (!File.Exists(archivo)) yield break;
            using (var fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var lector = new StreamReader(fs, Encoding.UTF8))
            {
                string linea;
                while ((linea = lector.ReadLine()) != null) yield return linea;
            }
        }

        /// <summary>Los ultimos eventos, el mas reciente primero.</summary>
        public List<EventoAuditoria> Ultimos(int cuantos)
        {
            var todos = new List<EventoAuditoria>();
            try
            {
                int n = 0;
                foreach (string linea in LeerLineas())
                {
                    n++;
                    string[] p = linea.Split('\t');
                    if (p.Length < 5) continue;
                    todos.Add(new EventoAuditoria { Fecha = p[0], Equipo = p[1], Usuario = p[2], Accion = p[3], Detalle = p[4], Linea = n });
                }
            }
            catch (Exception ex)
            {
                Log.Error("No se pudo leer la auditoria", ex);
            }
            todos.Reverse();
            return todos.Take(cuantos).ToList();
        }

        /// <summary>Revisa la cadena completa. Dice la primera linea alterada, si la hay.</summary>
        public ResultadoCadena VerificarCadena()
        {
            var r = new ResultadoCadena();
            string esperada = Inicio;
            int n = 0;
            try
            {
                foreach (string linea in LeerLineas())
                {
                    n++;
                    if (linea.Trim().Length == 0) continue;
                    string[] p = linea.Split('\t');
                    if (p.Length != 7)
                    {
                        r.Integra = false;
                        r.Detalle = "La linea " + n + " tiene un formato invalido.";
                        break;
                    }
                    if (p[5] != esperada)
                    {
                        r.Integra = false;
                        r.Detalle = "La cadena se rompe en la linea " + n + ": falta o se altero una linea anterior.";
                        break;
                    }
                    string cuerpo = string.Join("\t", p, 0, 6);
                    if (Hashes.Sha256Texto(cuerpo) != p[6])
                    {
                        r.Integra = false;
                        r.Detalle = "La linea " + n + " fue modificada.";
                        break;
                    }
                    esperada = p[6];
                }
            }
            catch (Exception ex)
            {
                r.Integra = false;
                r.Detalle = "No se pudo leer: " + ex.Message;
            }
            r.Lineas = n;
            if (r.Integra) r.Detalle = "Bitacora integra (" + n.ToString(CultureInfo.InvariantCulture) + " registros).";
            return r;
        }

        private static string Limpiar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "-";
            return texto.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
