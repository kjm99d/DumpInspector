namespace DumpInspector.Server.Services.Interfaces
{
    public interface IPdbProvider
    {
        /// <summary>
        /// Try to get the PDB bytes for the given pdbName/path. Return null if not found.
        /// </summary>
        Task<byte[]?> GetPdbAsync(string pdbName);
    }
}
