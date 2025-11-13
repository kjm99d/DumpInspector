namespace DumpInspector.Server.Models
{
    public record PdbUploadResult(
        string SymbolStoreRoot,
        string Product,
        string? Version,
        string OriginalFileName,
        string SymStoreCommand,
        string SymStoreOutput);
}
