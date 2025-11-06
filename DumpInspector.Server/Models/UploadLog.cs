namespace DumpInspector.Server.Models
{
    public class UploadLog
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string FileName { get; set; } = default!;
        public long FileSize { get; set; }
        public string? IpAddress { get; set; }
        public DateTime UploadedAt { get; set; }
        public string AnalysisSummary { get; set; } = string.Empty;
        public string AnalysisJson { get; set; } = string.Empty;
    }
}
