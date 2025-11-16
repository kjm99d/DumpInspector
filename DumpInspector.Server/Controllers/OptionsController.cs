using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DumpInspector.Server.Controllers
{
    /// <summary>
    /// 애플리케이션 옵션(CrashDumpSettings)을 노출하고 갱신한다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OptionsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly DumpInspector.Server.Data.AppDbContext _db;

        public OptionsController(IConfiguration config, DumpInspector.Server.Data.AppDbContext db)
        {
            _config = config;
            _db = db;
        }

        /// <summary>
        /// CrashDumpSettings 구성을 가져온다.
        /// </summary>
        [HttpGet("CrashDumpSettings")]
        [ProducesResponseType(typeof(CrashDumpSettings), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCrashDumpSettings()
        {
            var key = "CrashDumpSettings";
            var opt = await _db.Options.AsNoTracking().FirstOrDefaultAsync(o => o.Key == key);
            if (opt != null && !string.IsNullOrWhiteSpace(opt.Value))
            {
                try
                {
                    var obj = JsonSerializer.Deserialize<DumpInspector.Server.Models.CrashDumpSettings>(opt.Value, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (obj != null) return Ok(obj);
                }
                catch
                {
                    // fall through to file/config
                }
            }

            var file = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (System.IO.File.Exists(file))
            {
                try
                {
                    var txt = System.IO.File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.TryGetProperty("CrashDumpSettings", out var section))
                    {
                        var obj = section.Deserialize<DumpInspector.Server.Models.CrashDumpSettings>(new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (obj != null) return Ok(obj);
                    }
                }
                catch
                {
                    // ignore and fallback to config
                }
            }

            var fallback = _config.GetSection("CrashDumpSettings").Get<DumpInspector.Server.Models.CrashDumpSettings>();
            return Ok(fallback);
        }

        /// <summary>
        /// CrashDumpSettings 구성을 저장한다.
        /// </summary>
        [HttpPut("CrashDumpSettings")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PutCrashDumpSettings([FromBody] DumpInspector.Server.Models.CrashDumpSettings model)
        {
            // Very small persistence: update appsettings.json in the project folder and also store in Options table
            var file = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!System.IO.File.Exists(file)) 
                return NotFound("appsettings.json not found");
            var txt = await System.IO.File.ReadAllTextAsync(file);
            var root = JsonSerializer.Deserialize<Dictionary<string, object?>>(txt) ?? new Dictionary<string, object?>();
            // replace CrashDumpSettings
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            root["CrashDumpSettings"] = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var outTxt = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(file, outTxt);

            // store in DB Options
            var key = "CrashDumpSettings";
            var opt = _db.Options.FirstOrDefault(o => o.Key == key);
            if (opt == null)
            {
                opt = new AppOption { Key = key, Value = json };
                _db.Options.Add(opt);
            }
            else
            {
                opt.Value = json;
                _db.Options.Update(opt);
            }
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
