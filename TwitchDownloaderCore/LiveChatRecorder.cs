using System.Collections.Concurrent;
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
        private readonly ITaskProgress _progress;

        private readonly string _cacheDir;
        private readonly DirectoryInfo _emoteCache;
        private readonly DirectoryInfo _badgeCacheDir;

        public LiveChatRecorder(LiveChatRecorderOptions recorderOptions, ITaskProgress progress)
        {
            _recorderOptions = recorderOptions;
            _progress = progress;

            _cacheDir = CacheDirectoryService.GetCacheDirectory("");
            _emoteCache = new DirectoryInfo(Path.Combine(_cacheDir, "emotes"));
            _badgeCacheDir = new DirectoryInfo(Path.Combine(_cacheDir, "badges"));
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

            if (_recorderOptions.Duration is not null && _recorderOptions.NextStream)
            {
                throw new ArgumentException("Can't set both a duration and next-stream");
            }

            await RecordAsyncImpl(cancellationToken);
        }

        private async Task RecordAsyncImpl(CancellationToken cancellationToken)
        {
            var outputFileInfo = TwitchHelper.ClaimFile(_recorderOptions.OutputFile, _recorderOptions.FileCollisionCallback, _progress);
            _recorderOptions.OutputFile = outputFileInfo.FullName;

            try
            {
                var streamerId = (await TwitchHelper.GetUserIds([_recorderOptions.Channel])).data.users[0].id;


                if (_recorderOptions.NextStream)
                {
                    using var eventHub = new TwitchEventHub(_progress);
                    var streamInfo = await TwitchHelper.GetLiveStreamInfo(_recorderOptions.Channel);

                    if (streamInfo.data.stream is null)
                    {
                        try
                        {
                            await WaitFor(StreamStateChange.START, eventHub, streamerId, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, new CancellationTokenSource(new TimeSpan(0, 1, 0)).Token).Token);
                        }
                        catch { }
                    }
                }


                // TODO: distinguish between task ending regularly signal and cancellation
                // also this obv does not yet wait for stream end and uses 1min instead
                var _cancellationSource = new CancellationTokenSource(_recorderOptions.Duration ?? new TimeSpan(0, 1, 0));
                var chatRoot = await ProcessMessages(CancellationTokenSource.CreateLinkedTokenSource(_cancellationSource.Token, cancellationToken).Token);

                using (var outputStream = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await ChatJson.SerializeAsync(outputStream, chatRoot, cancellationToken);
                }
            }
            catch
            {
                await Task.Delay(100);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, null, _progress);

                throw;
            }
        }

        private async Task<ChatRoot> ProcessMessages(CancellationToken cancellationToken)
        {
            ConcurrentQueue<Comment> Comments = new();

            var firstPartyEmoteLoader = new FirstParteEmoteLoader(_progress, _emoteCache);
            var badgeLoader = new BadgeLoader(0, _progress, _badgeCacheDir);

            var chatRoot = new ChatRoot
            {
                FileInfo = new ChatRootInfo { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.Now },
                streamer = new Streamer(),
                video = new Video(),
                comments = new List<Comment>(),
                embeddedData = new EmbeddedData()
            };

            await foreach (var message in TwitchIrcClient.MessagesFor(_recorderOptions.Channel, cancellationToken, _progress).ReadAllAsync())
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

                    firstPartyEmoteLoader.ProcessComment(comment);
                    badgeLoader.ProcessComment(comment);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            chatRoot.comments = Comments.ToList();
            chatRoot.embeddedData.firstParty = await firstPartyEmoteLoader.GetList();
            chatRoot.embeddedData.twitchBadges = await badgeLoader.GetList();

            return chatRoot;
        }

        private enum StreamStateChange { START, END }
        private async Task WaitFor(StreamStateChange target, TwitchEventHub eventHub, string streamerId, CancellationToken cancellationToken)
        {
            using var sub = await eventHub.SubscribeTo(streamerId, [TwitchEventHub.TwitchChatEvent.VideoPlaybackById]);

            var eventTarget = target switch { StreamStateChange.START => "stream-up", StreamStateChange.END => "stream-down", _ => throw new ArgumentException("invalid StreamStateChange", "target") };

            await foreach (var msg in sub.Messages.ReadAllAsync(cancellationToken))
            {
                if (((NotificationData)msg.Data).pubsub.Contains(eventTarget))
                {
                    break;
                }
            }
        }
    }
}