using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public class LiveChatRecorder : IDisposable
    {
        private readonly LiveChatRecorderOptions _recorderOptions;
        private readonly ITaskProgress _progress;
        private readonly TwitchIrcClient _ircClient;

        public readonly ConcurrentQueue<Comment> Comments = new();

        public LiveChatRecorder(LiveChatRecorderOptions recorderOptions, ITaskProgress progress)
        {
            _recorderOptions = recorderOptions;
            _progress = progress;
            _ircClient = new TwitchIrcClient(progress);

            recorderOptions.StopRecording += (_, _) =>
            {
                _progress.LogInfo("Stopping recording...");

                _ = _ircClient.DisconnectAsync(CancellationToken.None);
            };
        }

        public async Task RecordAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_recorderOptions.Channel))
            {
                throw new NullReferenceException("Channel name cannot be null or empty.");
            }

            if (_recorderOptions.Channel.Contains(' ') || !_recorderOptions.Channel.All(char.IsAscii))
            {
                throw new ArgumentException("Invalid channel name.");
            }

            var outputFileInfo = TwitchHelper.ClaimFile(_recorderOptions.OutputFile, _recorderOptions.FileCollisionCallback, _progress);
            _recorderOptions.OutputFile = outputFileInfo.FullName;

            _ircClient.DebugFile = outputFileInfo;

            try
            {
                await RecordAsyncImpl(cancellationToken);
            }
            catch
            {
                await Task.Delay(100, cancellationToken);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, null, _progress);

                throw;
            }
        }

        private async Task RecordAsyncImpl(CancellationToken cancellationToken)
        {
            await _ircClient.ConnectAsync(cancellationToken);
            await _ircClient.JoinChannelAsync(_recorderOptions.Channel, cancellationToken);

            await ProcessMessages(cancellationToken);

            await _ircClient.LeaveChannelAsync(cancellationToken);
            await _ircClient.DisconnectAsync(cancellationToken);
        }

        private async Task ProcessMessages(CancellationToken cancellationToken)
        {
            do
            {
                await foreach (var message in _ircClient.GetNewMessagesAsync(cancellationToken))
                {
                    try
                    {
                        var res = IrcMessageConverter.ToComment(message);
                        if (res is null)
                        {
                            _progress.LogWarning($"Failed to convert message: {message}");
                            continue;
                        }

                        Comments.Enqueue(res);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

                await Task.Delay(50, cancellationToken);
            } while (_ircClient.IsConnected || _ircClient.HasNewMessages);
        }

        public void Dispose()
        {
            _ircClient?.Dispose();
        }
    }
}