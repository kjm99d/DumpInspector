using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DumpInspector.Server.Services.Implementations
{
    public class CdbAnalysisService : IAnalysisService
    {
        private readonly IPdbProvider _pdbProvider;
        private readonly ICrashDumpSettingsProvider _settingsProvider;
        private readonly ILogger<CdbAnalysisService> _logger;

        public CdbAnalysisService(IPdbProvider pdbProvider,
            ICrashDumpSettingsProvider settingsProvider,
            ILogger<CdbAnalysisService> logger)
        {
            _pdbProvider = pdbProvider;
            _settingsProvider = settingsProvider;
            _logger = logger;
        }

        public async Task<DumpAnalysisResult> AnalyzeAsync(string storedFilePath, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var fi = new FileInfo(storedFilePath);
            var summary = new StringBuilder();
            var detailedOutput = new StringBuilder();
            var succeeded = false;

            try
            {
                var settings = await _settingsProvider.GetAsync(cancellationToken);
                var cdbPath = ResolveCdbPath(settings);
                var symbolPath = BuildSymbolPath(settings);
                var timeout = Math.Max(10, settings.AnalysisTimeoutSeconds);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));
                var token = timeoutCts.Token;

                if (!IsExecutableAvailable(cdbPath))
                {
                    throw new FileNotFoundException($"cdb 실행 파일을 찾을 수 없습니다: {cdbPath}");
                }

                var args = BuildArguments(symbolPath, storedFilePath);

                var psi = new ProcessStartInfo
                {
                    FileName = cdbPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrWhiteSpace(symbolPath))
                {
                    psi.Environment["_NT_SYMBOL_PATH"] = symbolPath;
                }

                using var process = new Process { StartInfo = psi };

                var outputTask = new TaskCompletionSource<bool>();
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        outputTask.TrySetResult(true);
                    }
                    else
                    {
                        detailedOutput.AppendLine(e.Data);
                        progress?.Report(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        detailedOutput.AppendLine(e.Data);
                        progress?.Report(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                progress?.Report("cdb 분석을 시작합니다...");

                await process.WaitForExitAsync(token);
                await outputTask.Task;

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                succeeded = process.ExitCode == 0;
                summary.AppendLine(succeeded ? "cdb 분석 완료" : $"cdb 분석 실패 (종료 코드 {process.ExitCode})");
            }
            catch (OperationCanceledException)
            {
                summary.AppendLine("분석이 취소되었습니다.");
                detailedOutput.AppendLine("분석 취소: 시간 초과 또는 사용자 요청");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "cdb 분석 중 오류");
                summary.AppendLine($"분석 실패: {ex.Message}");
            }

            var report = detailedOutput.ToString().Trim();
            if (string.IsNullOrWhiteSpace(report))
            {
                // fall back to simple summary if cdb output empty
                var pdbSummary = await TrySummarizeWithPdb(fi.Name);
                if (!string.IsNullOrWhiteSpace(pdbSummary))
                {
                    summary.AppendLine(pdbSummary);
                }
            }

            return new DumpAnalysisResult
            {
                FileName = fi.Name,
                SizeBytes = fi.Length,
                AnalyzedAt = DateTime.UtcNow,
                Summary = summary.ToString().Trim(),
                DetailedReport = string.IsNullOrWhiteSpace(report) ? null : report
            };
        }

        private async Task<string?> TrySummarizeWithPdb(string dumpFileName)
        {
            try
            {
                var pdbName = Path.GetFileNameWithoutExtension(dumpFileName) + ".pdb";
                var pdbBytes = await _pdbProvider.GetPdbAsync(pdbName);
                return pdbBytes == null ? "PDB not found" : $"PDB found ({pdbBytes.Length} bytes)";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "PDB 요약 생성 중 오류");
                return null;
            }
        }

        private string BuildArguments(string? symbolPath, string dumpPath)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(symbolPath))
            {
                builder.Append($"-y \"{symbolPath}\" ");
            }

            builder.Append($"-z \"{dumpPath}\" ");
            builder.Append("-c \"!analyze -v; q\"");
            return builder.ToString();
        }

        private string? BuildSymbolPath(CrashDumpSettings settings)
        {
            var paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.SymbolPath))
            {
                paths.Add(settings.SymbolPath.Trim());
            }

            var env = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
            if (!string.IsNullOrWhiteSpace(env))
            {
                paths.Add(env);
            }

            return paths.Count == 0 ? null : string.Join(";", paths.Distinct());
        }

        private string ResolveCdbPath(CrashDumpSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.CdbPath) && IsExecutableAvailable(settings.CdbPath))
            {
                return settings.CdbPath!;
            }

            foreach (var candidate in GetDefaultCdbCandidates())
            {
                if (IsExecutableAvailable(candidate)) return candidate;
            }

            return settings.CdbPath ?? "cdb";
        }

        private IEnumerable<string> GetDefaultCdbCandidates()
        {
            var candidates = new List<string>();

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                candidates.Add(Path.Combine(programFilesX86, @"Windows Kits\10\Debuggers\x64\cdb.exe"));
                candidates.Add(Path.Combine(programFilesX86, @"Windows Kits\10\Debuggers\x86\cdb.exe"));
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, @"Windows Kits\10\Debuggers\x64\cdb.exe"));
                candidates.Add(Path.Combine(programFiles, @"Windows Kits\10\Debuggers\x86\cdb.exe"));
            }

            candidates.Add("cdb.exe");
            candidates.Add("cdb");
            return candidates;
        }

        private bool IsExecutableAvailable(string pathOrCommand)
        {
            if (File.Exists(pathOrCommand)) return true;

            try
            {
                var envPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                var extensions = new[] { ".exe", ".bat", ".cmd" };
                foreach (var dir in envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    foreach (var ext in extensions)
                    {
                        var candidate = Path.Combine(dir.Trim(), pathOrCommand.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? pathOrCommand : pathOrCommand + ext);
                        if (File.Exists(candidate)) return true;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }
}
