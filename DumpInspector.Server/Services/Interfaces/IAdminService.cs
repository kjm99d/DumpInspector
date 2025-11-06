namespace DumpInspector.Server.Services.Interfaces
{
    public interface IAdminService
    {
        Task<bool> VerifyAdminSecretAsync(string? secret);
    }
}
