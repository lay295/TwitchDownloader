using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    public sealed class TwitchSocketClient : IDisposable
    {
        private const int RECEIVE_BUFFER_SIZE = 4096;

        public bool SocketOpen => _socket.State is WebSocketState.Open;
        public FileInfo DebugFile { get; set; }

        public event EventHandler<(byte[] Buffer, WebSocketMessageType MessageType)> MessageReceived;

        private readonly ITaskLogger _logger;

        private ClientWebSocket _socket;
        private Uri _connectedUri;
        private FileStream _debugFile;

        public TwitchSocketClient(ITaskLogger logger)
        {
            _logger = logger;
            _socket = new ClientWebSocket();
        }

        public async Task<bool> ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (SocketOpen)
            {
                if (_connectedUri == uri)
                    return true;

                _logger.LogWarning($"Tried to change socket uri without disconnecting. {_connectedUri} -> {uri}.");
                return false;
            }

            if (_socket.State is not WebSocketState.Open and not WebSocketState.Connecting)
            {
                _socket.Abort();
                _socket = new ClientWebSocket();
            }

            try
            {
                _logger.LogVerbose($"Connecting to {uri}...");

                await _socket.ConnectAsync(uri, cancellationToken);
                _connectedUri = uri;

                _ = Task.Factory.StartNew(async () => await ReceiveMessages(cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                return SocketOpen;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to connect to {uri}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DisconnectAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_socket.State is WebSocketState.Open or WebSocketState.Connecting)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                    _socket.Dispose();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to disconnect socket: {ex.Message}");
                return false;
            }
        }

        private async Task ReceiveMessages(CancellationToken cancellationToken)
        {
            var receiveBuff = ArrayPool<byte>.Shared.Rent(RECEIVE_BUFFER_SIZE);

            try
            {
                _logger.LogVerbose($"Listening for messages from {_connectedUri}...");

                while (SocketOpen)
                {
                    var (messageBuff, messageType) = await ReceiveMessageBuffer(receiveBuff, cancellationToken);

                    switch (messageType)
                    {
                        case WebSocketMessageType.Text:
                        case WebSocketMessageType.Binary:
                            if (messageBuff.Length == 0)
                                continue;

                            WriteToDebugFile(messageBuff, "vvv"u8, true);

                            MessageReceived?.Invoke(this, (messageBuff, messageType));
                            break;
                        case WebSocketMessageType.Close:
                            WriteToDebugFile(messageBuff, "vvv"u8, true);

                            MessageReceived?.Invoke(this, (messageBuff, messageType));
                            return;
                        default:
                            _logger.LogWarning($"Received unknown message type: {(int)messageType}.");
                            break;
                    }
                }
            }
            finally
            {
                _logger.LogVerbose($"Stopped listening for messages from {_connectedUri}.");

                ArrayPool<byte>.Shared.Return(receiveBuff, true);

                try
                {
                    if (_debugFile != null)
                    {
                        await _debugFile.DisposeAsync();
                        _debugFile = null;
                    }
                }
                catch { }
            }
        }

        private async Task<(byte[] MessageBuff, WebSocketMessageType MessageType)> ReceiveMessageBuffer(byte[] receiveBuff, CancellationToken cancellationToken)
        {
            var messageBuff = Array.Empty<byte>();
            var messageLen = 0;

            ValueWebSocketReceiveResult receiveResult;
            do
            {
                try
                {
                    receiveResult = await _socket.ReceiveAsync(receiveBuff.AsMemory(), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return (messageBuff, WebSocketMessageType.Close);
                }

                // Grow the message buffer if needed
                if (messageLen + receiveResult.Count > messageBuff.Length)
                {
                    var newLen = receiveResult.EndOfMessage
                        ? messageBuff.Length + receiveResult.Count
                        : messageBuff.Length + RECEIVE_BUFFER_SIZE;

                    Array.Resize(ref messageBuff, newLen);
                }

                // Copy the received data into the message buffer
                receiveBuff.AsSpan(0, receiveResult.Count).CopyTo(messageBuff.AsSpan(messageLen));

                messageLen += receiveResult.Count;
            } while (!receiveResult.EndOfMessage);

            return (messageBuff, receiveResult.MessageType);
        }

        public ValueTask SendTextAsync(string str, CancellationToken cancellation)
        {
            if (!SocketOpen)
                return ValueTask.CompletedTask;

            var bytes = Encoding.UTF8.GetBytes(str);

            return SendTextAsync(bytes, cancellation);
        }

        public ValueTask SendTextPooledAsync(string str, CancellationToken cancellationToken, bool sensitive = false)
        {
            if (!SocketOpen)
                return ValueTask.CompletedTask;

            var byteCount = Encoding.UTF8.GetByteCount(str);
            var bytes = ArrayPool<byte>.Shared.Rent(byteCount);
            var written = Encoding.UTF8.GetBytes(str, bytes);

            Debug.Assert(byteCount == written);

            return new ValueTask(SendAndReturn(this, bytes, byteCount, cancellationToken, sensitive));

            static async Task SendAndReturn(TwitchSocketClient client, byte[] bytes, int byteCount, CancellationToken cancellationToken, bool sensitive)
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
            if (!SocketOpen)
                return ValueTask.CompletedTask;

            WriteToDebugFile(str.Span, "^^^"u8);

            return _socket.SendAsync(str, WebSocketMessageType.Text, true, cancellation);
        }

        public ValueTask SendBinaryAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation)
        {
            if (!SocketOpen)
                return ValueTask.CompletedTask;

            WriteToDebugFile(message.Span, "^^^"u8);

            return _socket.SendAsync(message, WebSocketMessageType.Binary, true, cancellation);
        }

        private void WriteToDebugFile(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> prefix, bool flush = false)
        {
            if (DebugFile is null || bytes.IsEmpty)
            {
                return;
            }

            lock (DebugFile)
            {
                try
                {
                    _debugFile ??= new FileStream(DebugFile.FullName, FileMode.Append, FileAccess.Write, FileShare.Read);

                    _debugFile.Write(prefix);
                    _debugFile.Write(" "u8);
                    _debugFile.Write(bytes.TrimEnd("\r\n"u8));
                    _debugFile.Write("\r\n"u8);
                    if (flush) _debugFile.Flush();
                }
                catch (Exception ex)
                {
                    _logger.LogVerbose($"Failed to write to debug file: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _socket?.Dispose();
            _debugFile?.Dispose();
            _debugFile = null;
        }
    }
}