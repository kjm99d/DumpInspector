using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using System.Text.Json;

namespace DumpInspector.Server.Services.Implementations
{
    public class JsonUserRepository : IUserRepository
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1,1);

        public JsonUserRepository(IConfiguration config)
        {
            var baseDir = AppContext.BaseDirectory;
            var dataDir = Path.Combine(baseDir, "data");
            Directory.CreateDirectory(dataDir);
            _filePath = Path.Combine(dataDir, "users.json");
        }

        public async Task EnsureInitializedAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    await File.WriteAllTextAsync(_filePath, "[]");
                }
            }
            finally { _lock.Release(); }
        }

        private async Task<List<User>> ReadAllAsync()
        {
            await EnsureInitializedAsync();
            await _lock.WaitAsync();
            try
            {
                var txt = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<List<User>>(txt) ?? new List<User>();
            }
            finally { _lock.Release(); }
        }

        private async Task WriteAllAsync(List<User> users)
        {
            await _lock.WaitAsync();
            try
            {
                var txt = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, txt);
            }
            finally { _lock.Release(); }
        }

        public async Task CreateAsync(User user)
        {
            var users = await ReadAllAsync();
            if (users.Any(u => u.Username == user.Username)) throw new InvalidOperationException("User exists");
            users.Add(user);
            await WriteAllAsync(users);
        }

        public async Task<IEnumerable<User>> GetAllAsync() => await ReadAllAsync();

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var users = await ReadAllAsync();
            return users.FirstOrDefault(u => u.Username == username);
        }

        public async Task UpdateAsync(User user)
        {
            var users = await ReadAllAsync();
            var idx = users.FindIndex(u => u.Username == user.Username);
            if (idx >= 0) users[idx] = user;
            else users.Add(user);
            await WriteAllAsync(users);
        }

        public async Task DeleteAsync(string username)
        {
            var users = await ReadAllAsync();
            var removed = users.RemoveAll(u => u.Username == username);
            if (removed > 0)
            {
                await WriteAllAsync(users);
            }
        }
    }
}
