using DumpInspector.Server.Models;
using System.Threading;

namespace DumpInspector.Server.Services.Interfaces
{
    public interface IAnalysisService
    {
        Task<DumpAnalysisResult> AnalyzeAsync(string storedFilePath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    }
}
