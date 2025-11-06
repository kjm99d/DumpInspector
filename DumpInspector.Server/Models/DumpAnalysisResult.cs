namespace DumpInspector.Server.Models
{
    public class DumpAnalysisResult
    {
        public string FileName { get; set; } = default!;
        public long SizeBytes { get; set; }
        public DateTime AnalyzedAt { get; set; }
        public string Summary { get; set; } = default!;
        public string? DetailedReport { get; set; }
    }
}
