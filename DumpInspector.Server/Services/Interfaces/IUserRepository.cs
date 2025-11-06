using DumpInspector.Server.Models;

namespace DumpInspector.Server.Services.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<IEnumerable<User>> GetAllAsync();
        Task CreateAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(string username);
        Task EnsureInitializedAsync();
    }
}
