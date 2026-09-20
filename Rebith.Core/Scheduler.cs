using System;

namespace Rebith.Core
{
    /// <summary>
    /// Calcula cuando toca cada respaldo programado. La proxima ejecucion se calcula
    /// siempre a partir de la ULTIMA ejecucion real y de la hora elegida, asi:
    ///  - no se va recorriendo la hora dia con dia,
    ///  - si la PC estuvo apagada a la hora programada, corre en cuanto se pueda (una vez),
    ///  - cambiar la hora en la pantalla se respeta de inmediato.
    /// </summary>
    public static class Programador
    {
        public static DateTime? ProximaEjecucion(TargetInfo t, DateTime ahora)
        {
            ScheduleInfo s = t.Schedule;
            if (s == null || s.Frequency == Frecuencias.Manual || string.IsNullOrEmpty(s.Frequency)) return null;

            DateTime? ultima = Tiempo.Leer(s.LastRunLocal);
            DateTime desde = ultima ?? (Tiempo.Leer(t.CreatedLocal) ?? ahora);
            return SiguienteTurno(s, desde);
        }

        /// <summary>El primer turno programado estrictamente despues de "desde".</summary>
        public static DateTime SiguienteTurno(ScheduleInfo s, DateTime desde)
        {
            DateTime candidato = new DateTime(desde.Year, desde.Month, desde.Day, s.Hour, s.Minute, 0);
            if (s.Frequency == Frecuencias.Semanal)
            {
                int dias = ((s.DayOfWeek - (int)candidato.DayOfWeek) + 7) % 7;
                candidato = candidato.AddDays(dias);
                if (candidato <= desde) candidato = candidato.AddDays(7);
            }
            else
            {
                if (candidato <= desde) candidato = candidato.AddDays(1);
            }
            return candidato;
        }

        public static bool TocaAhora(TargetInfo t, DateTime ahora)
        {
            DateTime? proxima = ProximaEjecucion(t, ahora);
            if (!proxima.HasValue || proxima.Value > ahora) return false;
            DateTime? reintento = Tiempo.Leer(t.Schedule.RetryAfterLocal);
            return !reintento.HasValue || reintento.Value <= ahora;
        }

        public static bool TocaRevisionAutomatica(RepoSettings ajustes, DateTime ahora)
        {
            if (ajustes.AutoVerifyDays <= 0) return false;
            DateTime? ultima = Tiempo.Leer(ajustes.LastAutoVerifyLocal);
            return !ultima.HasValue || ultima.Value.AddDays(ajustes.AutoVerifyDays) <= ahora;
        }

        public static string Describir(TargetInfo t)
        {
            ScheduleInfo s = t.Schedule;
            if (s == null || s.Frequency == Frecuencias.Manual) return "Manual";
            string hora = s.Hour.ToString("00") + ":" + s.Minute.ToString("00");
            if (s.Frequency == Frecuencias.Semanal)
            {
                string[] dias = { "domingo", "lunes", "martes", "miercoles", "jueves", "viernes", "sabado" };
                return "Cada " + dias[Math.Max(0, Math.Min(6, s.DayOfWeek))] + " a las " + hora;
            }
            return "Diario a las " + hora;
        }
    }
}
