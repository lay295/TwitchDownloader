using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
    public sealed class TwitchIrcClient : IDisposable
    {
        private const int FALSE = 0;
        private const int TRUE = 1;
        private const string ANONYMOUS_PASSWORD = "SCHMOOPIIE";
        private static readonly string AnonymousUsername = $"justinfan{Random.Shared.Next(10_000, 99_999)}";

        public FileInfo DebugFile
        {
            get => _client.DebugFile;
            set => _client.DebugFile = value;
        }

        public bool IsConnected => Reconnecting || _pingTimer != null;
        public bool HasNewMessages => !_messages.IsEmpty;

        private bool Reconnecting => _reconnecting != FALSE;

        private readonly ConcurrentQueue<IrcMessage> _messages;
        private readonly ITaskLogger _logger;
        private readonly TwitchSocketClient _client;
        private readonly IrcParser _ircParser;
        private readonly TimeSpan _pingInterval;

        private int _reconnecting;
        private string _joinedChannel;
        private Timer _pingTimer;
        private DateTimeOffset _lastMessage;

        public TwitchIrcClient(ITaskLogger logger)
        {
            _messages = new ConcurrentQueue<IrcMessage>();
            _logger = logger;
            _client = new TwitchSocketClient(logger);
            _client.MessageReceived += Client_OnMessageReceived;
            _ircParser = new IrcParser(logger);
            _pingInterval = TimeSpan.FromMinutes(5);
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
        {
            if (_client.SocketOpen)
            {
                return true;
            }

            var count = 0;
            const int MAX_TRIES = 8;
            while (true)
            {
                if (await _client.ConnectAsync(new Uri("wss://irc-ws.chat.twitch.tv/"), cancellationToken))
                    break;

                if (++count >= 5)
                {
                    _logger.LogWarning($"Failed to connect to Twitch IRC after {MAX_TRIES} tries.");
                    return false;
                }

                var sleepTime = GetExponentialBackoff(count);
                _logger.LogVerbose($"Failed to connect to Twitch IRC, retrying in {sleepTime:N0}ms...");
                await Task.Delay(sleepTime, cancellationToken);
            }

            _logger.LogInfo("Connected to Twitch IRC");

            await _client.SendTextPooledAsync("CAP REQ :twitch.tv/commands twitch.tv/tags", cancellationToken);
            await _client.SendTextPooledAsync($"PASS {ANONYMOUS_PASSWORD}", cancellationToken);
            await _client.SendTextPooledAsync($"NICK {AnonymousUsername}", cancellationToken);
            await _client.SendTextPooledAsync($"USER {AnonymousUsername} 8 * :{AnonymousUsername}", cancellationToken);

            _pingTimer = new Timer(PingTimerCallback, this, _pingInterval, _pingInterval);

            return true;

            static void PingTimerCallback(object state)
            {
                if (state is not TwitchIrcClient ircClient)
                    return;

                try
                {
                    ircClient.EnsureSocketConnected(CancellationToken.None).RunSynchronously();

                    ircClient._client.SendTextPooledAsync("PING", CancellationToken.None).RunSynchronously();

                    var messageTimeout = ircClient._pingInterval + TimeSpan.FromSeconds(10);
                    if (ircClient._lastMessage != default && DateTimeOffset.UtcNow - ircClient._lastMessage > messageTimeout)
                    {
                        ircClient._logger.LogWarning($"Last response exceeds {messageTimeout.TotalMilliseconds:F0}ms timeout ({ircClient._lastMessage:u}), initiating reconnect...");
                        ircClient.ReconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    ircClient._logger.LogError($"Error in IRC ping callback: {ex.Message}");
                }
            }
        }

        public async Task<bool> DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_pingTimer != null)
            {
                await _pingTimer.DisposeAsync();
                _pingTimer = null;
            }

            if (!_client.SocketOpen)
            {
                return true;
            }

            await LeaveChannelAsync(cancellationToken);

            var count = 0;
            const int MAX_TRIES = 8;
            while (true)
            {
                if (await _client.DisconnectAsync(cancellationToken))
                    break;

                if (++count >= MAX_TRIES)
                {
                    _logger.LogWarning($"Failed to disconnect from Twitch IRC after {MAX_TRIES} tries.");
                    return false;
                }

                var sleepTime = GetExponentialBackoff(count);
                _logger.LogVerbose($"Failed to disconnect from Twitch IRC, retrying in {sleepTime:N0}ms...");
                await Task.Delay(sleepTime, cancellationToken);
            }

            return true;
        }

        private static int GetExponentialBackoff(int count)
        {
            return (int)Math.Min(
                Math.Pow(2.25, count) * Random.Shared.Next(50, 100),
                30_000
            );
        }

        public async Task<bool> JoinChannelAsync(string channelName, CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                _logger.LogWarning($"Tried to join #{channelName}, but the socket was closed.");
                return false;
            }

            await EnsureSocketConnected(cancellationToken);

            _logger.LogVerbose($"Joining #{channelName}...");
            await _client.SendTextPooledAsync($"JOIN #{channelName}", cancellationToken);
            _joinedChannel = channelName;

            return true;
        }

        public async Task<bool> LeaveChannelAsync(CancellationToken cancellationToken)
        {
            if (!_client.SocketOpen)
            {
                return true;
            }

            if (_joinedChannel != null)
            {
                _logger.LogVerbose($"Leaving #{_joinedChannel}...");
                await _client.SendTextPooledAsync($"PART #{_joinedChannel}", cancellationToken);
                _joinedChannel = null;
            }

            return true;
        }

        public async Task<bool> ReconnectAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _reconnecting, TRUE) != FALSE)
                return false;

            _logger.LogInfo("Reconnecting to Twitch IRC...");

            bool success;
            try
            {
                var channelName = _joinedChannel;
                success = await LeaveChannelAsync(cancellationToken)
                          && await DisconnectAsync(cancellationToken)
                          && await ConnectAsync(cancellationToken)
                          && await JoinChannelAsync(channelName, cancellationToken);
            }
            finally
            {
                Interlocked.Exchange(ref _reconnecting, FALSE);
            }

            return success;
        }

        public async IAsyncEnumerable<IrcMessage> GetNewMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await EnsureSocketConnected(cancellationToken);

            while (_messages.TryDequeue(out var message))
            {
                yield return message;
            }
        }

        private ValueTask EnsureSocketConnected(CancellationToken cancellationToken)
        {
            if (IsConnected && !Reconnecting && !_client.SocketOpen)
            {
                return new ValueTask(ReconnectAsync(cancellationToken));
            }

            return ValueTask.CompletedTask;
        }

        private void Client_OnMessageReceived(object sender, TwitchSocketClient.Message e)
        {
            switch (e.MessageType)
            {
                case WebSocketMessageType.Binary:
                    _logger.LogWarning("Binary messages are not supported. See verbose log for more info.");
                    _logger.LogVerbose($"Binary message content: {Convert.ToBase64String(e.Buffer)}");
                    return;
                case WebSocketMessageType.Close:
                    if (!IsConnected)
                        return;

                    _logger.LogWarning("Lost connection to Twitch IRC, initiating reconnect...");
                    _ = ReconnectAsync(CancellationToken.None);
                    return;
            }

            var messages = _ircParser.Parse(e.Buffer);

            foreach (var ircMessage in messages)
            {
                _lastMessage = DateTimeOffset.UtcNow;

                switch (ircMessage.Command)
                {
                    case IrcCommand.Ping:
                        _client.SendTextPooledAsync(
                            ircMessage.ParametersRaw is { Length: > 0 } ? $"PONG {ircMessage.ParametersRaw}" : "PONG",
                            CancellationToken.None
                        );
                        break;
                    case IrcCommand.Reconnect:
                        _ = ReconnectAsync(CancellationToken.None);
                        break;
                    case IrcCommand.PrivMsg:
                    case IrcCommand.UserNotice:
                        _messages.Enqueue(ircMessage);
                        break;
                    case IrcCommand.Unknown:
                        _logger.LogWarning($"Got unknown IRC command: {Encoding.UTF8.GetString(e.Buffer).TrimEnd()}");
                        break;
                }
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _pingTimer?.Dispose();
            _pingTimer = null;
        }
    }
}