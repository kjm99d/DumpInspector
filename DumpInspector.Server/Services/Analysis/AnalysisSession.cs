using System;
using DumpInspector.Server.Models;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;

namespace DumpInspector.Server.Services.Analysis
{
    public class AnalysisSession
    {
        private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
        private readonly CancellationTokenSource _cts = new();
        private readonly Progress<string> _progress;

        public AnalysisSession()
        {
            Id = Guid.NewGuid().ToString("N");
            _progress = new Progress<string>(line =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                EnqueueMessage(new { type = "line", data = line });
            });

            EnqueueMessage(new { type = "info", message = "분석을 준비합니다..." });
        }

        public string Id { get; }

        public IProgress<string> Progress => _progress;

        public ChannelReader<string> Reader => _channel.Reader;

        public CancellationToken CancellationToken => _cts.Token;

        public void Complete(DumpAnalysisResult result)
        {
            EnqueueMessage(new
            {
                type = "complete",
                summary = result.Summary,
                detailedReport = result.DetailedReport,
                analyzedAt = result.AnalyzedAt,
                fileName = result.FileName
            });
            _channel.Writer.TryComplete();
        }

        public void Fail(string message)
        {
            EnqueueMessage(new { type = "error", message });
            _channel.Writer.TryComplete();
        }

        public void Cancel()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private void EnqueueMessage(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            _channel.Writer.TryWrite(json);
        }
    }
}
