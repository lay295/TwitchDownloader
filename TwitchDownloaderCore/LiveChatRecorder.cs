using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore
{
    public class LiveChatRecorder : IDisposable
    {
        private readonly LiveChatRecorderOptions _recorderOptions;
        private readonly ITaskProgress _progress;

        private readonly TwitchIrcClient _ircClient;

        public LiveChatRecorder(LiveChatRecorderOptions recorderOptions, ITaskProgress progress)
        {
            _recorderOptions = recorderOptions;
            _progress = progress;
            _ircClient = new TwitchIrcClient(progress);

            recorderOptions.StopRecording += (_, _) =>
            {
                _progress.LogInfo("Stopping recording...");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                _ircClient.DisconnectAsync(CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
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

            var outputFileInfo = TwitchHelper.ClaimFile(_recorderOptions.Filename, _recorderOptions.FileCollisionCallback, _progress);
            _recorderOptions.Filename = outputFileInfo.FullName;

            // Open the destination file so that it exists in the filesystem.
            await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

            try
            {
                await RecordAsyncImpl(outputFileInfo, outputFs, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, CancellationToken.None);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);

                throw;
            }
        }

        private async Task RecordAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, CancellationToken cancellationToken)
        {
            await _ircClient.ConnectAsync(cancellationToken);
            await _ircClient.JoinChannelAsync(_recorderOptions.Channel, cancellationToken);

            await PrintMessages(cancellationToken);

            await _ircClient.LeaveChannelAsync(cancellationToken);
            await _ircClient.DisconnectAsync(cancellationToken);
        }

        private async Task PrintMessages(CancellationToken cancellationToken)
        {
            do
            {
                while (_ircClient.Messages.TryDequeue(out var message))
                {
                    try
                    {
                        var res = IrcMessageConverter.ToComment(message);
                        if (res is null)
                        {
                            Console.WriteLine($"!!!! Failed to convert message: {message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

                await Task.Delay(50, cancellationToken);
            } while (_ircClient.IsConnected);
        }

        public void Dispose()
        {
            _ircClient?.Dispose();
        }
    }
}