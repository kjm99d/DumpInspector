using DumpInspector.Server.Data;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DumpInspector.Server.Services.Implementations
{
    public class EfUserRepository : IUserRepository
    {
        private readonly AppDbContext _db;

        public EfUserRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task CreateAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<User>> GetAllAsync() => await _db.Users.AsNoTracking().ToListAsync();

        public async Task<User?> GetByUsernameAsync(string username) => await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        public async Task UpdateAsync(User user)
        {
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(string username)
        {
            var entity = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (entity != null)
            {
                _db.Users.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task EnsureInitializedAsync()
        {
            await _db.Database.EnsureCreatedAsync();
        }
    }
}
