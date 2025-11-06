namespace DumpInspector.Server.Services.Interfaces
{
    public interface IDumpStorageService
    {
        /// <summary>
        /// Save an uploaded dump to the configured storage and return the saved filename (unique).
        /// </summary>
        Task<string> SaveDumpAsync(Stream content, string originalFileName);
        string GetDumpFolder();
    }
}
