using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace DumpInspector.Server.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _repo;

        public AuthService(IUserRepository repo)
        {
            _repo = repo;
        }

        public async Task CreateUserAsync(string username, string password, bool isAdmin = false, string? email = null)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Hash(password, salt);
            var user = new User
            {
                Username = username,
                IsAdmin = isAdmin,
                Email = email,
                Salt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(hash)
            };
            await _repo.CreateAsync(user);
        }

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            var user = await _repo.GetByUsernameAsync(username);
            if (user == null) return false;
            var salt = Convert.FromBase64String(user.Salt);
            var expected = Convert.FromBase64String(user.PasswordHash);
            var actual = Hash(password, salt);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        public async Task ResetPasswordAsync(string username, string newPassword)
        {
            var user = await _repo.GetByUsernameAsync(username) ?? throw new KeyNotFoundException("User not found");
            var salt = RandomNumberGenerator.GetBytes(16);
            user.Salt = Convert.ToBase64String(salt);
            user.PasswordHash = Convert.ToBase64String(Hash(newPassword, salt));
            await _repo.UpdateAsync(user);
        }

        public async Task<bool> IsAdminAsync(string username)
        {
            var user = await _repo.GetByUsernameAsync(username);
            return user?.IsAdmin ?? false;
        }

        public Task DeleteUserAsync(string username)
        {
            return _repo.DeleteAsync(username);
        }

        private static byte[] Hash(string password, byte[] salt)
        {
            using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }
    }
}
