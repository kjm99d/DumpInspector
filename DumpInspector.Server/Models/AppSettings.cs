namespace DumpInspector.Server.Models
{
    public class NasSettings
    {
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? RemotePdbPath { get; set; }
    }

    public class CrashDumpSettings
    {
        public string DumpStoragePath { get; set; } = "Dumps";
        public bool UseNasForPdb { get; set; } = false;
        public NasSettings? Nas { get; set; }
        public SmtpSettings? Smtp { get; set; }
        public string? CdbPath { get; set; }
        public string? SymbolPath { get; set; }
        public string? SymStorePath { get; set; }
        public string SymbolStoreRoot { get; set; } = "Symbols";
        public string SymbolStoreProduct { get; set; } = "DumpInspector";
        public long DumpUploadMaxBytes { get; set; } = 10L * 1024 * 1024 * 1024;
        public int AnalysisTimeoutSeconds { get; set; } = 120;
        public string? AdminSecret { get; set; }
        public string? InitialAdminPassword { get; set; }
    }

    public class SmtpSettings
    {
        public bool Enabled { get; set; } = false;
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? FromAddress { get; set; }
    }
}
