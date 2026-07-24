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
        private readonly CancellationTokenSource _cancellationSource;
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

            if (_recorderOptions.Duration is not null && _recorderOptions.NextStream)
            {
                throw new ArgumentException("Can't set both a duration and next-stream");
            }
            var outputFileInfo = TwitchHelper.ClaimFile(_recorderOptions.OutputFile, _recorderOptions.FileCollisionCallback, _progress);
            _recorderOptions.OutputFile = outputFileInfo.FullName;

            try
            {
                var chatRoot = await ProcessMessages();

                var outputStream = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
                await ChatJson.SerializeAsync(outputStream, chatRoot, cancellationToken);

                await testEventHubTask;
            }
            catch
            {
                await Task.Delay(100);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, null, _progress);

                throw;
            }
        }

        private async Task<ChatRoot> ProcessMessages()
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

        private async Task WaitForStreamStart(TwitchEventHub eventHub)
        {
            using var sub = await eventHub.SubscribeTo(_recorderOptions.Channel, [TwitchEventHub.TwitchChatEvent.VideoPlaybackById]);

            await foreach (var msg in sub.Messages.ReadAllAsync())
            {
                if (((NotificationData)msg.Data).pubsub.Contains("stream-up"))
                {
                    break;
                }
            }
        }

        private async Task TestEventHub()
        {
            using var eventHub = new TwitchEventHub(_progress);
            try
            {
                using var sub = await eventHub.SubscribeTo(_recorderOptions.Channel, [TwitchEventHub.TwitchChatEvent.VideoPlaybackById]);

                await foreach (var message in sub.Messages.ReadAllAsync(new CancellationTokenSource(_recorderOptions.Duration).Token))
                {
                    if (message.Data is not NotificationData)
                    {
                        _progress.LogInfo($"unexpected message type: {message.Data.GetType()}");
                        continue;
                    }
                    _progress.LogInfo($"Message: {((NotificationData)message.Data).pubsub}");
                }
            }
            catch { }
            _progress.LogInfo("end of readallasync");
            return;
        }
    }
}