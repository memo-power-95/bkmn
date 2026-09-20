using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Rebith.Core
{
    // Todos los modelos se guardan como JSON legible dentro del deposito.
    // Las fechas van como texto ISO (yyyy-MM-ddTHH:mm:ss) para que cualquiera
    // pueda abrir los archivos y entenderlos sin el programa.

    public static class Frecuencias
    {
        public const string Manual = "Manual";
        public const string Diario = "Diario";
        public const string Semanal = "Semanal";
    }

    [DataContract]
    public class ScheduleInfo
    {
        [DataMember(Order = 1)] public string Frequency { get; set; } = Frecuencias.Manual;
        [DataMember(Order = 2)] public int Hour { get; set; } = 2;
        [DataMember(Order = 3)] public int Minute { get; set; } = 0;
        /// <summary>0 = domingo ... 6 = sabado. Solo para Semanal.</summary>
        [DataMember(Order = 4)] public int DayOfWeek { get; set; } = 1;
        [DataMember(Order = 5)] public bool RunOnStartup { get; set; }
        /// <summary>Ultima vez que corrio el respaldo programado (hora local ISO). Vacio = nunca.</summary>
        [DataMember(Order = 6)] public string LastRunLocal { get; set; } = "";
        [DataMember(Order = 7)] public string LastResult { get; set; } = "";
        /// <summary>Si el ultimo intento fallo, no reintentar antes de esta hora (local ISO).</summary>
        [DataMember(Order = 8)] public string RetryAfterLocal { get; set; } = "";

        [OnDeserializing]
        private void Predeterminados(StreamingContext c)
        {
            Frequency = Frecuencias.Manual;
            Hour = 2;
            DayOfWeek = 1;
            LastRunLocal = "";
            LastResult = "";
            RetryAfterLocal = "";
        }
    }

    [DataContract]
    public class TargetInfo
    {
        [DataMember(Order = 1)] public string Id { get; set; } = "";
        [DataMember(Order = 2)] public string Name { get; set; } = "";
        [DataMember(Order = 3)] public string SourcePath { get; set; } = "";
        [DataMember(Order = 4)] public bool IsFolder { get; set; } = true;
        [DataMember(Order = 5)] public List<string> Excludes { get; set; } = new List<string>();
        [DataMember(Order = 6)] public ScheduleInfo Schedule { get; set; } = new ScheduleInfo();
        /// <summary>Cuantos respaldos conservar. 0 = todos.</summary>
        [DataMember(Order = 7)] public int KeepLast { get; set; }
        /// <summary>true = leer y calcular hash de todos los archivos siempre (mas lento, mas estricto).</summary>
        [DataMember(Order = 8)] public bool AlwaysReadAll { get; set; }
        [DataMember(Order = 9)] public string CreatedLocal { get; set; } = "";

        [OnDeserialized]
        private void Completar(StreamingContext c)
        {
            if (Excludes == null) Excludes = new List<string>();
            if (Schedule == null) Schedule = new ScheduleInfo();
            if (Name == null) Name = "";
            if (SourcePath == null) SourcePath = "";
        }
    }

    [DataContract]
    public class TargetList
    {
        [DataMember] public List<TargetInfo> Targets { get; set; } = new List<TargetInfo>();

        [OnDeserialized]
        private void Completar(StreamingContext c)
        {
            if (Targets == null) Targets = new List<TargetInfo>();
        }
    }

    [DataContract]
    public class FileEntry
    {
        /// <summary>Ruta relativa con "/" (igual que la version en Python).</summary>
        [DataMember(Order = 1)] public string Path { get; set; } = "";
        [DataMember(Order = 2)] public string Hash { get; set; } = "";
        [DataMember(Order = 3)] public long Size { get; set; }
        /// <summary>Fecha de modificacion en ticks UTC.</summary>
        [DataMember(Order = 4)] public long MtimeUtcTicks { get; set; }
    }

    [DataContract]
    public class SkippedEntry
    {
        [DataMember(Order = 1)] public string Path { get; set; } = "";
        [DataMember(Order = 2)] public string Reason { get; set; } = "";
    }

    public static class EstadosSnapshot
    {
        public const string Completo = "Completo";
        public const string ConAdvertencias = "Con advertencias";
    }

    [DataContract]
    public class SnapshotManifest
    {
        [DataMember(Order = 1)] public int FormatVersion { get; set; } = 1;
        [DataMember(Order = 2)] public string Id { get; set; } = "";
        [DataMember(Order = 3)] public string TargetId { get; set; } = "";
        [DataMember(Order = 4)] public string TargetName { get; set; } = "";
        [DataMember(Order = 5)] public string SourcePath { get; set; } = "";
        [DataMember(Order = 6)] public bool IsFolder { get; set; }
        [DataMember(Order = 7)] public string Note { get; set; } = "";
        [DataMember(Order = 8)] public string User { get; set; } = "";
        [DataMember(Order = 9)] public string Machine { get; set; } = "";
        [DataMember(Order = 10)] public string StartedLocal { get; set; } = "";
        [DataMember(Order = 11)] public string FinishedLocal { get; set; } = "";
        [DataMember(Order = 12)] public string Status { get; set; } = EstadosSnapshot.Completo;
        [DataMember(Order = 13)] public long TotalBytes { get; set; }
        [DataMember(Order = 14)] public int NewObjects { get; set; }
        [DataMember(Order = 15)] public long NewBytesStored { get; set; }
        /// <summary>SHA-256 de la lista de archivos. Detecta un manifiesto danado o alterado.</summary>
        [DataMember(Order = 16)] public string ContentHash { get; set; } = "";
        [DataMember(Order = 17)] public List<FileEntry> Files { get; set; } = new List<FileEntry>();
        [DataMember(Order = 18)] public List<SkippedEntry> Skipped { get; set; } = new List<SkippedEntry>();

        [OnDeserialized]
        private void Completar(StreamingContext c)
        {
            if (Files == null) Files = new List<FileEntry>();
            if (Skipped == null) Skipped = new List<SkippedEntry>();
            if (Note == null) Note = "";
            if (Status == null) Status = EstadosSnapshot.Completo;
        }
    }

    [DataContract]
    public class InProgressMarker
    {
        [DataMember(Order = 1)] public string SnapshotId { get; set; } = "";
        [DataMember(Order = 2)] public string TargetId { get; set; } = "";
        [DataMember(Order = 3)] public string StartedLocal { get; set; } = "";
        [DataMember(Order = 4)] public string Machine { get; set; } = "";
        [DataMember(Order = 5)] public int ProcessId { get; set; }
    }

    [DataContract]
    public class RepoInfo
    {
        [DataMember(Order = 1)] public int FormatVersion { get; set; } = 1;
        [DataMember(Order = 2)] public string RepoId { get; set; } = "";
        [DataMember(Order = 3)] public string CreatedLocal { get; set; } = "";
        [DataMember(Order = 4)] public string CreatedBy { get; set; } = "";
    }

    [DataContract]
    public class RepoSettings
    {
        /// <summary>Segunda copia del deposito (otro disco o red). Vacio = sin replica.</summary>
        [DataMember(Order = 1)] public string ReplicaPath { get; set; } = "";
        /// <summary>Cada cuantos dias revisar automaticamente la integridad de todo el deposito. 0 = nunca.</summary>
        [DataMember(Order = 2)] public int AutoVerifyDays { get; set; } = 7;
        [DataMember(Order = 3)] public string LastAutoVerifyLocal { get; set; } = "";
        [DataMember(Order = 4)] public string LastAutoVerifyResult { get; set; } = "";
        /// <summary>No iniciar un respaldo si queda menos que esto libre en el disco del deposito.</summary>
        [DataMember(Order = 5)] public int MinFreeSpaceMB { get; set; } = 1024;
        [DataMember(Order = 6)] public int SessionTimeoutMinutes { get; set; } = 15;
        [DataMember(Order = 7)] public bool LastAutoVerifyOk { get; set; } = true;

        // El serializador no ejecuta el constructor: si al archivo le falta un campo
        // (version vieja), estos son los valores que quedan.
        [OnDeserializing]
        private void Predeterminados(StreamingContext c)
        {
            ReplicaPath = "";
            AutoVerifyDays = 7;
            LastAutoVerifyLocal = "";
            LastAutoVerifyResult = "";
            MinFreeSpaceMB = 1024;
            SessionTimeoutMinutes = 15;
            LastAutoVerifyOk = true;
        }
    }

    [DataContract]
    public class UserInfo
    {
        [DataMember(Order = 1)] public string Username { get; set; } = "";
        [DataMember(Order = 2)] public string PasswordHash { get; set; } = "";
        [DataMember(Order = 3)] public string Role { get; set; } = Roles.Operador;
        [DataMember(Order = 4)] public bool IsActive { get; set; } = true;
        [DataMember(Order = 5)] public bool CanBackup { get; set; } = true;
        [DataMember(Order = 6)] public bool CanRestore { get; set; } = true;
        [DataMember(Order = 7)] public bool CanDelete { get; set; }
        [DataMember(Order = 8)] public bool CanManageUsers { get; set; }
        [DataMember(Order = 9)] public bool CanManageSecurity { get; set; }
        [DataMember(Order = 10)] public bool MustChangePassword { get; set; }
        [DataMember(Order = 11)] public int FailedAttempts { get; set; }
        [DataMember(Order = 12)] public string LockedUntilLocal { get; set; } = "";
        [DataMember(Order = 13)] public string CreatedLocal { get; set; } = "";

        public bool EsAdmin { get { return Role == Roles.Admin; } }

        [OnDeserializing]
        private void Predeterminados(StreamingContext c)
        {
            Role = Roles.Operador;
            IsActive = true;
            CanBackup = true;
            CanRestore = true;
            PasswordHash = "";
            LockedUntilLocal = "";
            CreatedLocal = "";
        }

        public UserInfo Clonar()
        {
            return (UserInfo)MemberwiseClone();
        }
    }

    public static class Roles
    {
        public const string Operador = "operador";
        public const string Supervisor = "supervisor";
        public const string Admin = "admin";
        public static readonly string[] Todos = { Operador, Supervisor, Admin };
    }

    [DataContract]
    public class UserList
    {
        [DataMember] public List<UserInfo> Users { get; set; } = new List<UserInfo>();

        [OnDeserialized]
        private void Completar(StreamingContext c)
        {
            if (Users == null) Users = new List<UserInfo>();
        }
    }

    [DataContract]
    public class MachineSettings
    {
        [DataMember(Order = 1)] public string RepoPath { get; set; } = "";
    }

    [DataContract]
    public class VerifiedObjects
    {
        /// <summary>hash -> fecha local ISO de la ultima verificacion correcta.</summary>
        [DataMember] public Dictionary<string, string> LastOk { get; set; } = new Dictionary<string, string>();

        [OnDeserialized]
        private void Completar(StreamingContext c)
        {
            if (LastOk == null) LastOk = new Dictionary<string, string>();
        }
    }

    /// <summary>Error esperado con mensaje claro para el usuario (no es un fallo del programa).</summary>
    public class RebithException : Exception
    {
        public RebithException(string message) : base(message) { }
        public RebithException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Otra operacion ya tiene el deposito ocupado.</summary>
    public class RepoBusyException : RebithException
    {
        public RepoBusyException(string message) : base(message) { }
    }

    public class ProgressInfo
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Item { get; set; } = "";
        public string Stage { get; set; } = "";
    }
}
