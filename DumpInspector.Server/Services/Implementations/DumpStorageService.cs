using DumpInspector.Server.Services.Interfaces;

namespace DumpInspector.Server.Services.Implementations
{
    public class DumpStorageService : IDumpStorageService
    {
        private readonly string _dumpFolder;

        public DumpStorageService(Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings> options)
        {
            var cfg = options.Value;
            _dumpFolder = cfg.DumpStoragePath ?? Path.Combine(AppContext.BaseDirectory, "Dumps");
            if (!Path.IsPathRooted(_dumpFolder)) _dumpFolder = Path.Combine(AppContext.BaseDirectory, _dumpFolder);
            Directory.CreateDirectory(_dumpFolder);
        }

        public string GetDumpFolder() => _dumpFolder;

        public async Task<string> SaveDumpAsync(Stream content, string originalFileName)
        {
            // Ensure unique filename: timestamp + guid + original
            var safeName = Path.GetFileName(originalFileName) ?? "dump.dmp";
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{safeName}";
            var full = Path.Combine(_dumpFolder, fileName);
            using var fs = File.Create(full);
            await content.CopyToAsync(fs);
            return full;
        }
    }
}
