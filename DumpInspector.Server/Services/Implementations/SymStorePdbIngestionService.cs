using System;
using DumpInspector.Server.Models;
using DumpInspector.Server.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;

namespace DumpInspector.Server.Services.Implementations
{
    public class SymStorePdbIngestionService : IPdbIngestionService
    {
        private readonly IOptionsSnapshot<CrashDumpSettings> _options;
        private readonly ILogger<SymStorePdbIngestionService> _logger;

        public SymStorePdbIngestionService(
            IOptionsSnapshot<CrashDumpSettings> options,
            ILogger<SymStorePdbIngestionService> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task<PdbUploadResult> IngestAsync(
            string pdbFilePath,
            string originalFileName,
            string productName,
            string? version,
            string? comment,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pdbFilePath) || !File.Exists(pdbFilePath))
            {
                throw new FileNotFoundException("PDB 파일을 찾을 수 없습니다.", pdbFilePath);
            }

            var settings = _options.Value;
            var symStoreExe = ResolveSymStorePath(settings);
            var symbolStoreRoot = ResolveSymbolStoreRoot(settings);
            Directory.CreateDirectory(symbolStoreRoot);

            var psi = BuildProcessStartInfo(symStoreExe, symbolStoreRoot, pdbFilePath, productName, version, comment);
            var commandLine = BuildCommandPreview(symStoreExe, psi.ArgumentList);
            var outputBuilder = new StringBuilder();

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("symstore 프로세스를 시작하지 못했습니다.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
#if NET8_0_OR_GREATER
                await process.WaitForExitAsync(cancellationToken);
#else
                await Task.Run(() => process.WaitForExit(), cancellationToken);
#endif

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"symstore.exe 실패 (코드 {process.ExitCode}). 출력: {outputBuilder}");
                }
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* ignored */ }
                }
                throw;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError(ex, "symstore 실행 실패");
                throw new InvalidOperationException($"symstore.exe 실행 실패: {ex.Message}. CrashDumpSettings.SymStorePath를 확인하세요.", ex);
            }

            return new PdbUploadResult(
                symbolStoreRoot,
                productName,
                version,
                Path.GetFileName(originalFileName),
                commandLine,
                outputBuilder.ToString().Trim());
        }

        private static ProcessStartInfo BuildProcessStartInfo(
            string symStoreExe,
            string symbolStoreRoot,
            string pdbFilePath,
            string productName,
            string? version,
            string? comment)
        {
            var psi = new ProcessStartInfo(symStoreExe)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("add");
            psi.ArgumentList.Add("/f");
            psi.ArgumentList.Add(pdbFilePath);
            psi.ArgumentList.Add("/s");
            psi.ArgumentList.Add(symbolStoreRoot);
            psi.ArgumentList.Add("/t");
            psi.ArgumentList.Add(string.IsNullOrWhiteSpace(productName) ? "DumpInspector" : productName);

            if (!string.IsNullOrWhiteSpace(version))
            {
                psi.ArgumentList.Add("/v");
                psi.ArgumentList.Add(version);
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(comment);
            }

            return psi;
        }

        private static string ResolveSymStorePath(CrashDumpSettings settings)
        {
            var configured = settings.SymStorePath;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var expanded = Environment.ExpandEnvironmentVariables(configured);
                if (File.Exists(expanded))
                {
                    return expanded;
                }
            }

            foreach (var candidate in GetDefaultSymStoreCandidates())
            {
                if (File.Exists(candidate)) return candidate;
            }

            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return "symstore.exe";
        }

        private static IEnumerable<string> GetDefaultSymStoreCandidates()
        {
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                yield return Path.Combine(programFilesX86, @"Windows Kits\10\Debuggers\x64\symstore.exe");
                yield return Path.Combine(programFilesX86, @"Windows Kits\10\Debuggers\x86\symstore.exe");
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return Path.Combine(programFiles, @"Windows Kits\10\Debuggers\x64\symstore.exe");
                yield return Path.Combine(programFiles, @"Windows Kits\10\Debuggers\x86\symstore.exe");
            }
        }

        private static string ResolveSymbolStoreRoot(CrashDumpSettings settings)
        {
            var root = settings.SymbolStoreRoot;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(AppContext.BaseDirectory, "Symbols");
            }

            root = Environment.ExpandEnvironmentVariables(root);
            if (!Path.IsPathRooted(root))
            {
                root = Path.Combine(AppContext.BaseDirectory, root);
            }

            return root;
        }

        private static string BuildCommandPreview(string exePath, IReadOnlyList<string> args)
        {
            var sb = new StringBuilder();
            sb.Append(QuoteIfNeeded(exePath));
            foreach (var arg in args)
            {
                sb.Append(' ').Append(QuoteIfNeeded(arg));
            }
            return sb.ToString();
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            if (value.Contains(' ') || value.Contains('\t') || value.Contains('"'))
            {
                return $"\"{value.Replace("\"", "\\\"")}\"";
            }
            return value;
        }
    }
}
