using DumpInspector.Server.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DumpInspector.Server.Services.Interfaces
{
    public interface IPdbIngestionService
    {
        Task<PdbUploadResult> IngestAsync(
            string pdbFilePath,
            string originalFileName,
            string productName,
            string? version,
            string? comment,
            CancellationToken cancellationToken = default);
    }
}
