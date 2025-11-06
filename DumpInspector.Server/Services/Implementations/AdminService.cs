using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;

namespace DumpInspector.Server.Services.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly DumpInspector.Server.Models.CrashDumpSettings _cfg;
        public AdminService(Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings> options)
        {
            _cfg = options.Value;
        }

        public Task<bool> VerifyAdminSecretAsync(string? secret)
        {
            if (string.IsNullOrEmpty(_cfg.AdminSecret)) return Task.FromResult(false);
            return Task.FromResult(secret == _cfg.AdminSecret);
        }
    }
}
