using DumpInspector.Server.Data;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Analysis;
using DumpInspector.Server.Services.Interfaces;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DumpInspector.Server.Controllers
{
    /// <summary>
    /// 덤프 파일 업로드와 분석 요청을 처리한다.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DumpController : ControllerBase
    {
        private readonly IDumpStorageService _storage;
        private readonly AnalysisSessionManager _sessionManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DumpController> _logger;
        private readonly ICrashDumpSettingsProvider _settingsProvider;
        private const long DefaultUploadLimitBytes = 10L * 1024 * 1024 * 1024;

        public DumpController(
            IDumpStorageService storage,
            AnalysisSessionManager sessionManager,
            IServiceScopeFactory scopeFactory,
            ILogger<DumpController> logger,
            ICrashDumpSettingsProvider settingsProvider)
        {
            _storage = storage;
            _sessionManager = sessionManager;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _settingsProvider = settingsProvider;
        }

        /// <summary>
        /// 덤프 파일을 업로드하고 백그라운드 분석 세션을 시작한다.
        /// </summary>
        /// <param name="file">업로드할 덤프 파일.</param>
        /// <param name="uploadedBy">요청자 메모(선택).</param>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(DumpUploadAcceptedResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status413PayloadTooLarge)]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? uploadedBy)
        {
            if (file == null || file.Length == 0) return BadRequest("no file");

            var settings = await _settingsProvider.GetAsync(HttpContext.RequestAborted);
            var maxBytes = settings.DumpUploadMaxBytes > 0 ? settings.DumpUploadMaxBytes : DefaultUploadLimitBytes;
            if (file.Length > maxBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge,
                    $"파일 크기가 허용된 최대 용량({FormatBytes(maxBytes)})을 초과했습니다.");
            }

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

            return Accepted(new DumpUploadAcceptedResponse(session.Id, fi.Name, fi.Length));
        }

        /// <summary>
        /// 저장된 덤프 파일 목록을 반환한다.
        /// </summary>
        [HttpGet("list")]
        [ProducesResponseType(typeof(IEnumerable<DumpFileEntry>), StatusCodes.Status200OK)]
        public IActionResult List()
        {
            var folder = _storage.GetDumpFolder();
            var files = Directory.GetFiles(folder)
                .Select(p => new DumpFileEntry(Path.GetFileName(p), new FileInfo(p).Length));
            return Ok(files);
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            if (bytes >= GB) return $"{bytes / (double)GB:0.##} GB";
            if (bytes >= MB) return $"{bytes / (double)MB:0.##} MB";
            if (bytes >= KB) return $"{bytes / (double)KB:0.##} KB";
            return $"{bytes} bytes";
        }
    }
}
