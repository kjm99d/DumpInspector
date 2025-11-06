using DumpInspector.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DumpInspector.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<AppOption> Options => Set<AppOption>();
        public DbSet<UploadLog> UploadLogs => Set<UploadLog>();
    }
}
