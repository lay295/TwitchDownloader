using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    public sealed class EventingWebSocket : IDisposable
    {
        public readonly record struct Message(byte[] Buffer, WebSocketMessageType MessageType);

        private readonly ITaskLogger _logger;
        private readonly ClientWebSocket _socket = new();
        private CancellationTokenSource _receiveLoopCts;
        private Task _receiveLoopTask;

        public event EventHandler<Message> MessageReceived;

        public EventingWebSocket(ITaskLogger logger)
        {
            _logger = logger;
        }

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            _logger.LogVerbose($"Connecting to {uri}...");
            await _socket.ConnectAsync(uri, cancellationToken);

            _receiveLoopCts = new CancellationTokenSource();
            _receiveLoopTask = ReceiveLoopAsync(uri, _receiveLoopCts.Token);
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            return _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
        }

        private async Task ReceiveLoopAsync(Uri uri, CancellationToken cancellationToken)
        {
            _logger.LogVerbose($"Listening for messages from {uri}...");
            var buffer = new byte[4096];

            try
            {
                while (_socket.State is WebSocketState.Open or WebSocketState.CloseSent)
                {
                    using var messageBuffer = new MemoryStream();

                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        messageBuffer.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var message = new Message { MessageType = result.MessageType, Buffer = messageBuffer.ToArray() };

                    // ignore errors in the handler
                    try { MessageReceived?.Invoke(this, message); } catch {}
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError($"websocket receive loop failed unexpectedly: {ex.GetType()} {ex.Message}");
            }
            finally
            {
                _logger.LogVerbose($"Stopped listening for messages from {uri}.");
            }
        }

        public ValueTask SendTextPooledAsync(string str, CancellationToken cancellationToken, bool sensitive = false)
        {
            var byteCount = Encoding.UTF8.GetByteCount(str);
            var bytes = ArrayPool<byte>.Shared.Rent(byteCount);
            var written = Encoding.UTF8.GetBytes(str, bytes);

            return new ValueTask(SendAndReturn(this, bytes, byteCount, cancellationToken, sensitive));

            static async Task SendAndReturn(EventingWebSocket client, byte[] bytes, int byteCount, CancellationToken cancellationToken, bool sensitive)
            {
                try
                {
                    await client.SendTextAsync(bytes.AsMemory(0, byteCount), cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes, sensitive);
                }
            }
        }

        public ValueTask SendTextAsync(ReadOnlyMemory<byte> str, CancellationToken cancellation)
        {
            return _socket.SendAsync(str, WebSocketMessageType.Text, true, cancellation);
        }

        public ValueTask SendBinaryAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation)
        {
            return _socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellation);
        }

        public void Dispose()
        {
            _receiveLoopCts?.Cancel();

            try
            {
                _receiveLoopTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch { }

            _receiveLoopCts?.Dispose();
            _socket.Dispose();
        }
    }
}
