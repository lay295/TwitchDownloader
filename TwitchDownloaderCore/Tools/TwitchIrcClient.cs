using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
    // TODO: Twitch website IRC client sends a PING exactly every 5 minutes
    public sealed class TwitchIrcClient : IDisposable
    {
        private static readonly string AnonymousUsername = $"justinfan{Random.Shared.Next(10_000, 99_999)}";

        public bool IsConnected => _client.SocketOpen;

        public readonly ConcurrentQueue<byte[]> Messages = new();

        private readonly ITaskLogger _logger;
        private readonly TwitchSocketClient _client;
        // private readonly IrcParser _ircParser;

        private string _joinedChannel;

        public TwitchIrcClient(ITaskLogger logger)
        {
            _logger = logger;
            _client = new TwitchSocketClient(logger);
            _client.MessageReceived += Client_OnMessageReceived;
            // _ircParser = new IrcParser(logger);
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
        {
            if (_client.SocketOpen)
            {
                _logger.LogWarning("Tried to connect to Twitch IRC multiple times.");
                return true;
            }

            var count = 0;
            const int MAX_TRIES = 5;
            while (true)
            {
                if (await _client.ConnectAsync(new Uri("wss://irc-ws.chat.twitch.tv/"), cancellationToken))
                    break;

                if (++count >= 5)
                {
                    _logger.LogWarning($"Failed to connect to Twitch IRC after {MAX_TRIES} tries.");
                    return false;
                }

                await Task.Delay(250 * count, cancellationToken);
            }

            _logger.LogVerbose("Connected to Twitch IRC");

            await _client.SendTextAsync("CAP REQ :twitch.tv/commands twitch.tv/tags", cancellationToken);
            // await _client.SendTextAsync("PASS SCHMOOPIIE", cancellationToken);
            await _client.SendTextAsync($"NICK {AnonymousUsername}", cancellationToken);
            // await _client.SendTextAsync($"USER {AnonymousUsername} 8 * :{AnonymousUsername}", cancellationToken);

            return true;
        }

        public async Task<bool> JoinChannelAsync(string channelName, CancellationToken cancellationToken)
        {
            if (!_client.SocketOpen)
            {
                _logger.LogWarning($"Tried to join #{channelName}, but the socket was closed.");
                return false;
            }

            _logger.LogVerbose($"Joining #{channelName}...");

            await _client.SendTextAsync($"JOIN #{channelName}", cancellationToken);
            _joinedChannel = channelName;

            return true;
        }

        public async Task<bool> DisconnectAsync(CancellationToken cancellationToken)
        {
            if (!_client.SocketOpen)
            {
                _logger.LogWarning("Tried to disconnect from an already closed socket.");
                return true;
            }

            if (_joinedChannel != null)
            {
                _logger.LogVerbose($"Leaving #{_joinedChannel}...");
                await _client.SendTextAsync($"PART #{_joinedChannel}", cancellationToken);
            }

            var count = 0;
            const int MAX_TRIES = 5;
            while (true)
            {
                if (await _client.DisconnectAsync(cancellationToken))
                    break;

                if (++count >= MAX_TRIES)
                {
                    _logger.LogWarning($"Failed to disconnect from Twitch IRC after {MAX_TRIES} tries.");
                    return false;
                }

                await Task.Delay(250 * count, cancellationToken);
            }

            return true;
        }

        private void Client_OnMessageReceived(object sender, (byte[] Buffer, WebSocketMessageType MessageType) e)
        {
            if (e.MessageType is not WebSocketMessageType.Text)
            {
                _logger.LogWarning("Binary messages are not supported. Enable verbose logging for more info.");
                _logger.LogVerbose($"Binary message content: {Convert.ToHexString(e.Buffer)}");
                return;
            }

            // var messages = _ircParser.Parse(e.Buffer);

            var str = Encoding.UTF8.GetString(e.Buffer);
            Console.WriteLine(str.Trim());
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}