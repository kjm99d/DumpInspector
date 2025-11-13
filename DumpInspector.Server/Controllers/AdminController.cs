using DumpInspector.Server.Data;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DumpInspector.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IUserRepository _users;
        private readonly AppDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IPdbIngestionService _pdbIngestion;
        private readonly IOptionsSnapshot<CrashDumpSettings> _options;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAuthService auth,
            IUserRepository users,
            AppDbContext db,
            IEmailSender emailSender,
            IPdbIngestionService pdbIngestion,
            IOptionsSnapshot<CrashDumpSettings> options,
            ILogger<AdminController> logger)
        {
            _auth = auth;
            _users = users;
            _db = db;
            _emailSender = emailSender;
            _pdbIngestion = pdbIngestion;
            _options = options;
            _logger = logger;
        }

        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                return BadRequest("Username is required");
            }
            if (string.IsNullOrWhiteSpace(req.Email))
            {
                return BadRequest("Email is required");
            }

            try
            {
                var makeAdmin = false;
                var email = req.Email.Trim();
                var username = req.Username.Trim();
                var tempPassword = GenerateTemporaryPassword();
                await _auth.CreateUserAsync(username, tempPassword, makeAdmin, email);

                try
                {
                    var subject = "[DumpInspector] 임시 비밀번호 안내";
                    var body = $"안녕하세요,\n\nDumpInspector 계정이 생성되었습니다.\n\n아이디: {username}\n임시 비밀번호: {tempPassword}\n\n로그인 후 비밀번호를 즉시 변경해 주세요.\n\n감사합니다.";
                    await _emailSender.SendAsync(email, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send welcome email to {Email}", email);
                    await _auth.DeleteUserAsync(username);
                    var reason = ex.InnerException?.Message ?? ex.Message;
                    return StatusCode(StatusCodes.Status500InternalServerError, $"사용자는 생성되었으나 이메일 발송에 실패했습니다. SMTP 설정을 확인하세요. (사유: {reason})");
                }

                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var list = await _users.GetAllAsync();
            var sanitized = list.Select(u => new UserSummary(u.Username, u.IsAdmin, u.Email));
            return Ok(sanitized);
        }

        [HttpDelete("users/{username}")]
        public async Task<IActionResult> DeleteUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return BadRequest("Username required");
            var user = await _users.GetByUsernameAsync(username);
            if (user == null) return NotFound("User not found");
            if (user.IsAdmin) return BadRequest("관리자 계정은 삭제할 수 없습니다.");

            await _auth.DeleteUserAsync(username);
            return NoContent();
        }

        [HttpPost("force-reset")]
        public async Task<IActionResult> ForceReset([FromBody] ForceResetRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                return BadRequest("Username required");
            }
            var tempPassword = GenerateTemporaryPassword();
            try
            {
                await _auth.ResetPasswordAsync(req.Username, tempPassword);
                return Ok(new { temporaryPassword = tempPassword });
            }
            catch (KeyNotFoundException)
            {
                return NotFound("User not found");
            }
        }

        [HttpGet("logs")]
        public IActionResult GetUploadLogs([FromQuery] int take = 100)
        {
            var logs = _db.UploadLogs
                .OrderByDescending(l => l.UploadedAt)
                .Take(Math.Clamp(take, 1, 500))
                .Select(l => new UploadLogDto(
                    l.Id,
                    l.Username,
                    l.FileName,
                    l.FileSize,
                    l.IpAddress,
                    l.UploadedAt,
                    l.AnalysisSummary,
                    l.AnalysisJson));

            return Ok(logs);
        }

        [HttpPost("upload-pdb")]
        public async Task<IActionResult> UploadPdb([FromForm] IFormFile file, [FromForm] string? productName, [FromForm] string? version, [FromForm] string? comment)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("PDB 파일을 선택하세요.");
            }

            if (!file.FileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("확장자가 .pdb인 파일만 업로드할 수 있습니다.");
            }

            var product = string.IsNullOrWhiteSpace(productName) ? _options.Value.SymbolStoreProduct : productName!.Trim();
            var ver = string.IsNullOrWhiteSpace(version) ? null : version!.Trim();
            var commentValue = string.IsNullOrWhiteSpace(comment) ? null : comment!.Trim();

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}");
            await using (var fs = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(fs);
            }

            try
            {
                var result = await _pdbIngestion.IngestAsync(
                    tempPath,
                    file.FileName,
                    product,
                    ver,
                    commentValue,
                    HttpContext.RequestAborted);

                return Ok(new
                {
                    message = "PDB 업로드 및 심볼 스토어 등록이 완료되었습니다.",
                    result.SymbolStoreRoot,
                    result.Product,
                    result.Version,
                    result.OriginalFileName,
                    result.SymStoreCommand,
                    result.SymStoreOutput
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(StatusCodes.Status499ClientClosedRequest, "요청이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDB 업로드 실패");
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
            finally
            {
                try { System.IO.File.Delete(tempPath); } catch { /* ignore */ }
            }
        }

        private static string GenerateTemporaryPassword(int length = 12)
        {
            const string allowed = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@$%^&*";
            Span<byte> buffer = stackalloc byte[length];
            RandomNumberGenerator.Fill(buffer);
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                sb.Append(allowed[buffer[i] % allowed.Length]);
            }
            return sb.ToString();
        }
    }

    public record CreateUserRequest(string Username, string Email);
    public record ForceResetRequest(string Username);
    public record UserSummary(string Username, bool IsAdmin, string? Email);
    public record UploadLogDto(int Id, string? Username, string FileName, long FileSize, string? IpAddress, DateTime UploadedAt, string Summary, string AnalysisJson);
}
