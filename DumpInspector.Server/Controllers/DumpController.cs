using DumpInspector.Server.Data;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Analysis;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DumpInspector.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DumpController : ControllerBase
    {
        private readonly IDumpStorageService _storage;
        private readonly AnalysisSessionManager _sessionManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DumpController> _logger;

        public DumpController(
            IDumpStorageService storage,
            AnalysisSessionManager sessionManager,
            IServiceScopeFactory scopeFactory,
            ILogger<DumpController> logger)
        {
            _storage = storage;
            _sessionManager = sessionManager;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string? uploadedBy)
        {
            if (file == null || file.Length == 0) return BadRequest("no file");

            using var s = file.OpenReadStream();
            var saved = await _storage.SaveDumpAsync(s, file.FileName);
            var fi = new FileInfo(saved);
            var session = _sessionManager.CreateSession();
            session.Progress.Report($"덤프 저장 완료: {fi.Name}");

            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userName = string.IsNullOrWhiteSpace(uploadedBy) ? null : uploadedBy.Trim();

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var analysis = scope.ServiceProvider.GetRequiredService<IAnalysisService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<DumpController>>();

                try
                {
                    var result = await analysis.AnalyzeAsync(saved, session.Progress, session.CancellationToken);

                    var logEntry = new UploadLog
                    {
                        Username = userName,
                        FileName = fi.Name,
                        FileSize = fi.Length,
                        IpAddress = remoteIp,
                        UploadedAt = DateTime.UtcNow,
                        AnalysisSummary = result.Summary,
                        AnalysisJson = JsonSerializer.Serialize(result)
                    };
                    db.UploadLogs.Add(logEntry);
                    await db.SaveChangesAsync();

                    session.Complete(result);
                    _sessionManager.ScheduleCleanup(session.Id, TimeSpan.FromMinutes(10));
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Analysis cancelled for session {SessionId}", session.Id);
                    session.Fail("분석이 취소되었습니다.");
                    _sessionManager.ScheduleCleanup(session.Id, TimeSpan.FromMinutes(1));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Analysis failed for session {SessionId}", session.Id);
                    session.Fail($"분석 실패: {ex.Message}");
                    _sessionManager.ScheduleCleanup(session.Id, TimeSpan.FromMinutes(1));
                }
            });

            return Accepted(new { sessionId = session.Id, fileName = fi.Name, sizeBytes = fi.Length });
        }

        [HttpGet("list")]
        public IActionResult List()
        {
            var folder = _storage.GetDumpFolder();
            var files = Directory.GetFiles(folder).Select(p => new { Name = Path.GetFileName(p), Size = new FileInfo(p).Length });
            return Ok(files);
        }
    }
}
