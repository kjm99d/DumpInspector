using DumpInspector.Server.Models;

namespace DumpInspector.Server.Services.Interfaces
{
    public interface IAuthService
    {
        Task CreateUserAsync(string username, string password, bool isAdmin = false, string? email = null);
        Task<bool> ValidateCredentialsAsync(string username, string password);
        Task ResetPasswordAsync(string username, string newPassword);
        Task<bool> IsAdminAsync(string username);
        Task DeleteUserAsync(string username);
    }
}
