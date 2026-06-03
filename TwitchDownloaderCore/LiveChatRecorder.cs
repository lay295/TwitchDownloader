using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public class LiveChatRecorder : IDisposable
    {
        private readonly LiveChatRecorderOptions _recorderOptions;
        private readonly ITaskProgress _progress;
        private readonly TwitchIrcClient _ircClient;

        private readonly string _cacheDir;
        private readonly DirectoryInfo _emoteCache;

        public LiveChatRecorder(LiveChatRecorderOptions recorderOptions, ITaskProgress progress)
        {
            _recorderOptions = recorderOptions;
            _progress = progress;
            _ircClient = new TwitchIrcClient(progress);

            _cacheDir = CacheDirectoryService.GetCacheDirectory("");
            _emoteCache = new DirectoryInfo(Path.Combine(_cacheDir, "emotes"));

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

            var debugFileInfo = TwitchHelper.ClaimFile(_recorderOptions.OutputFile + ".debug.txt", _recorderOptions.FileCollisionCallback, _progress);
            _ircClient.DebugFile = debugFileInfo;

            try
            {
                var chatRoot = await RecordAsyncImpl(cancellationToken);

                var outputStream = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
                await ChatJson.SerializeAsync(outputStream, chatRoot, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, cancellationToken);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, null, _progress);
                TwitchHelper.CleanUpClaimedFile(debugFileInfo, null, _progress);

                throw;
            }
        }

        private async Task<ChatRoot> RecordAsyncImpl(CancellationToken cancellationToken)
        {
            await _ircClient.ConnectAsync(cancellationToken);
            await _ircClient.JoinChannelAsync(_recorderOptions.Channel, cancellationToken);

            var chatRoot = await ProcessMessages(cancellationToken);

            await _ircClient.LeaveChannelAsync(cancellationToken);
            await _ircClient.DisconnectAsync(cancellationToken);

            return chatRoot;
        }

        private async Task<ChatRoot> ProcessMessages(CancellationToken cancellationToken)
        {
            ConcurrentQueue<Comment> Comments = new();

            ConcurrentDictionary<string, Task<TwitchEmote>> firstPartyEmotes = new();

            var chatRoot = new ChatRoot
            {
                FileInfo = new ChatRootInfo { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.Now },
                streamer = new Streamer(),
                video = new Video(),
                comments = new List<Comment>(),
                embeddedData = new EmbeddedData()
            };

            do
            {
                await foreach (var message in _ircClient.GetNewMessagesAsync(cancellationToken))
                {
                    try
                    {
                        var comment = IrcMessageConverter.ToComment(message);
                        if (comment is null)
                        {
                            _progress.LogWarning($"Failed to convert message: {message}");
                            continue;
                        }

                        Comments.Enqueue(comment);

                        foreach (var emoticon in comment.message.emoticons)
                        {
                            _ = firstPartyEmotes.GetOrAdd(emoticon._id, emoticonId => TwitchHelper.GetFirstPartyEmote(emoticonId, _emoteCache, false, _progress, CancellationToken.None));
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }

                await Task.Delay(50, cancellationToken);
            } while (_ircClient.IsConnected || _ircClient.HasNewMessages);

            foreach (var kvp in firstPartyEmotes)
            {
                var emote = await kvp.Value;
                var newEmote = new EmbedEmoteData
                {
                    id = emote.Id,
                    imageScale = emote.ImageScale,
                    data = emote.ImageData,
                    width = emote.Width / emote.ImageScale,
                    height = emote.Height / emote.ImageScale,
                };

                chatRoot.embeddedData.firstParty.Add(newEmote);
            }

            chatRoot.comments = Comments.ToList();

            return chatRoot;
        }

        public void Dispose()
        {
            _ircClient?.Dispose();
        }
    }
}