using System;
using System.IO;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DumpInspector.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdbController : ControllerBase
    {
        private readonly IPdbIngestionService _pdbIngestion;
        private readonly IOptionsSnapshot<CrashDumpSettings> _options;
        private readonly ILogger<PdbController> _logger;

        public PdbController(
            IPdbIngestionService pdbIngestion,
            IOptionsSnapshot<CrashDumpSettings> options,
            ILogger<PdbController> logger)
        {
            _pdbIngestion = pdbIngestion;
            _options = options;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
            [FromForm] IFormFile file,
            [FromForm] string? productName,
            [FromForm] string? version,
            [FromForm] string? comment,
            [FromForm] string? uploadedBy)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("PDB 파일을 선택하세요.");
            }

            if (!file.FileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("확장자가 .pdb인 파일만 업로드할 수 있습니다.");
            }

            var product = string.IsNullOrWhiteSpace(productName)
                ? _options.Value.SymbolStoreProduct
                : productName!.Trim();
            var ver = string.IsNullOrWhiteSpace(version) ? null : version!.Trim();
            var commentValue = BuildComment(comment, uploadedBy);

            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);
            var tempPath = Path.Combine(tempFolder, Path.GetFileName(file.FileName));
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
                try
                {
                    if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
                    if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                }
                catch { /* ignore */ }
            }
        }

        private static string? BuildComment(string? comment, string? uploadedBy)
        {
            var trimmedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            var trimmedUploader = string.IsNullOrWhiteSpace(uploadedBy) ? null : uploadedBy.Trim();
            if (trimmedComment == null && trimmedUploader == null) return null;
            if (trimmedComment != null && trimmedUploader == null) return trimmedComment;
            if (trimmedComment == null && trimmedUploader != null) return $"Uploaded by {trimmedUploader}";
            return $"{trimmedComment} (Uploaded by {trimmedUploader})";
        }
    }
}
