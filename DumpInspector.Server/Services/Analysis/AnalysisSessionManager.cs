using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DumpInspector.Server.Services.Analysis
{
    public class AnalysisSessionManager
    {
        private readonly ConcurrentDictionary<string, AnalysisSession> _sessions = new();

        public AnalysisSession CreateSession()
        {
            var session = new AnalysisSession();
            _sessions[session.Id] = session;
            return session;
        }

        public bool TryGetSession(string id, out AnalysisSession session)
            => _sessions.TryGetValue(id, out session);

        public void Remove(string id)
        {
            _sessions.TryRemove(id, out _);
        }

        public void ScheduleCleanup(string id, TimeSpan delay)
        {
            Task.Run(async () =>
            {
                await Task.Delay(delay);
                Remove(id);
            });
        }

        public async Task StreamSessionAsync(AnalysisSession session, WebSocket socket, CancellationToken cancellationToken)
        {
            var sendTask = SendLoopAsync(session, socket, cancellationToken);
            var receiveTask = ReceiveLoopAsync(session, socket, cancellationToken);

            await Task.WhenAll(sendTask, receiveTask);

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Completed", CancellationToken.None);
            }

            Remove(session.Id);
        }

        private static async Task SendLoopAsync(AnalysisSession session, WebSocket socket, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var message in session.Reader.ReadAllAsync(cancellationToken))
                {
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        private static async Task ReceiveLoopAsync(AnalysisSession session, WebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new byte[4];

            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        session.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                session.Cancel();
            }
        }
    }
}
