using System;
using DumpInspector.Server.Data;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DumpInspector.Server.Services.Implementations
{
    public class DbCrashDumpSettingsProvider : ICrashDumpSettingsProvider
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DbCrashDumpSettingsProvider> _logger;

        public DbCrashDumpSettingsProvider(
            AppDbContext db,
            IConfiguration configuration,
            ILogger<DbCrashDumpSettingsProvider> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<CrashDumpSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            const string key = "CrashDumpSettings";

            try
            {
                var opt = await _db.Options.AsNoTracking().FirstOrDefaultAsync(o => o.Key == key, cancellationToken);
                if (opt != null && !string.IsNullOrWhiteSpace(opt.Value))
                {
                    try
                    {
                        var settings = JsonSerializer.Deserialize<CrashDumpSettings>(opt.Value, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (settings != null)
                        {
                            return settings;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize CrashDumpSettings from database.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load CrashDumpSettings from database.");
            }

            var fallback = _configuration.GetSection("CrashDumpSettings").Get<CrashDumpSettings>();
            return fallback ?? new CrashDumpSettings();
        }
    }
}
