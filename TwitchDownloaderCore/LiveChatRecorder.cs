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
    public class LiveChatRecorder
    {
        private readonly LiveChatRecorderOptions _recorderOptions;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly ITaskProgress _progress;

        private readonly string _cacheDir;
        private readonly DirectoryInfo _emoteCache;

        public LiveChatRecorder(LiveChatRecorderOptions recorderOptions, ITaskProgress progress)
        {
            _recorderOptions = recorderOptions;
            _progress = progress;

            _cacheDir = CacheDirectoryService.GetCacheDirectory("");
            _emoteCache = new DirectoryInfo(Path.Combine(_cacheDir, "emotes"));

            _cancellationSource = new CancellationTokenSource(_recorderOptions.NextStream ? new TimeSpan(0, 1, 0) : _recorderOptions.Duration);
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

            try
            {
                var chatRoot = await ProcessMessages();

                var outputStream = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
                await ChatJson.SerializeAsync(outputStream, chatRoot, cancellationToken);
            }
            catch
            {
                await Task.Delay(100);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, null, _progress);
                TwitchHelper.CleanUpClaimedFile(debugFileInfo, null, _progress);

                throw;
            }
        }

        private async Task<ChatRoot> ProcessMessages()
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

            await foreach (var message in TwitchIrcClient.MessagesFor(_recorderOptions.Channel, _cancellationSource.Token, _progress).ReadAllAsync())
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

            foreach (var emoteTask in firstPartyEmotes.Values)
            {
                var emote = await emoteTask;
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
    }
}