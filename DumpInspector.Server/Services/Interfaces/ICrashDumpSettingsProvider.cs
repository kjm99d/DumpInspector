using DumpInspector.Server.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DumpInspector.Server.Services.Interfaces
{
    public interface ICrashDumpSettingsProvider
    {
        Task<CrashDumpSettings> GetAsync(CancellationToken cancellationToken = default);
    }
}
