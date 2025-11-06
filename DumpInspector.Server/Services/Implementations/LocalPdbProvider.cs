using DumpInspector.Server.Services.Interfaces;

namespace DumpInspector.Server.Services.Implementations
{
    public class LocalPdbProvider : IPdbProvider
    {
        private readonly string _pdbFolder;

        public LocalPdbProvider(Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings> options)
        {
            var cfg = options.Value;
            _pdbFolder = cfg.Nas?.RemotePdbPath ?? Path.Combine(AppContext.BaseDirectory, "pdb");
            if (!Path.IsPathRooted(_pdbFolder)) _pdbFolder = Path.Combine(AppContext.BaseDirectory, _pdbFolder);
            Directory.CreateDirectory(_pdbFolder);
        }

        public async Task<byte[]?> GetPdbAsync(string pdbName)
        {
            var path = Path.Combine(_pdbFolder, pdbName);
            if (!File.Exists(path)) return null;
            return await File.ReadAllBytesAsync(path);
        }
    }
}
