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

            if (_recorderOptions.Duration.HasValue && _recorderOptions.NextStream)
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
                using var eventHub = new TwitchEventHub(_progress);


                if (_recorderOptions.NextStream)
                {
                    var streamInfo = await TwitchHelper.GetLiveStreamInfo(_recorderOptions.Channel);

                    if (streamInfo.data.stream is null)
                    {
                        // await WaitFor(StreamStateChange.START, eventHub, streamerId, cancellationToken);
                        // TODO: remove dev only TimeSpan before merging
                        try
                        {
                            await WaitFor(StreamStateChange.START, eventHub, streamerId, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, new CancellationTokenSource(new TimeSpan(0, 0, 10)).Token).Token);
                        }
                        catch { }
                    }
                }

                // TODO: recording(Start/End) could be more accurately assed from the stream start and end event
                // but how to handle durations/start recording during ongoing stream/ensuring sync with a potential video recording
                var recordingStartUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var stopSignal = GetEndOfRecordingSignal(eventHub, streamerId, cancellationToken);
                var chapterTask = GetChapters(eventHub, _recorderOptions.Channel, streamerId, stopSignal, cancellationToken);
                var chatRoot = await ProcessMessages(stopSignal, cancellationToken);

                var recordingEndUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // fill in and fix the chatRoot data

                chatRoot.video.chapters = ToVideoChapters(await chapterTask, recordingStartUnixMs, recordingEndUnixMs);

                chatRoot.comments.ForEach(comment =>
                {
                    var createdAtUnixMs = new DateTimeOffset(comment.created_at.ToUniversalTime()).ToUnixTimeMilliseconds();
                    // TODO: the data model is double and we do not receive them from twitch as ints, should they be rounded at all?
                    comment.content_offset_seconds = Math.Floor((createdAtUnixMs - recordingStartUnixMs) / 1000.0);
                });

                // TODO: these might have better ways to set them, i know that start and end are not meant to be set like this technically (unless full stream)
                chatRoot.video.created_at = DateTimeOffset.FromUnixTimeMilliseconds(recordingStartUnixMs).UtcDateTime;
                chatRoot.video.start = 0;
                chatRoot.video.end = (recordingEndUnixMs - recordingStartUnixMs) / 1000.0;
                chatRoot.video.length = (recordingEndUnixMs - recordingStartUnixMs) / 1000.0;
                chatRoot.video.game = chatRoot.video.chapters.First().gameDisplayName;
                chatRoot.streamer.id = int.Parse(streamerId);
                chatRoot.streamer.login = _recorderOptions.Channel;

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

        private async Task<ChatRoot> ProcessMessages(Task stopSignal, CancellationToken cancellationToken)
        {
            ConcurrentQueue<Comment> Comments = new();

            var firstPartyEmoteLoader = new FirstParteEmoteLoader(_progress, _emoteCache);
            var badgeLoader = new BadgeLoader(0, _progress, _badgeCacheDir);

            var chatRoot = new ChatRoot
            {
                FileInfo = new ChatRootInfo { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.UtcNow },
                streamer = new Streamer(),
                video = new Video(),
                comments = new List<Comment>(),
                embeddedData = new EmbeddedData()
            };

            await foreach (var message in TwitchIrcClient.MessagesFor(_recorderOptions.Channel, stopSignal, cancellationToken, _progress).ReadAllAsync())
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

        private async Task<List<(long startMillisecondsUTC, TwitchObjects.Gql.GameData gameData)>> GetChapters(TwitchEventHub eventHub, string streamer, string streamerId, Task stopSignal, CancellationToken cancellationToken)
        {
            using var sub = await eventHub.SubscribeTo(streamerId, [TwitchEventHub.TwitchChatEvent.BroadcastSettingsUpdate]);

            var chapters = new List<(long startMillisecondsUTC, TwitchObjects.Gql.GameData gameData)>();
            var initialSettingsTask = TwitchHelper.GetBroadcastSettings(streamer).ContinueWith(settings => (0L, settings.Result.data.user.broadcastSettings.game));

            while (true)
            {
                var readTask = sub.Messages.WaitToReadAsync(cancellationToken).AsTask();
                var completedTask = await Task.WhenAny(readTask, stopSignal);

                if (completedTask == stopSignal)
                    break;

                if (!await readTask)
                    // the await is mostly so that internal errors are thrown, but if the code enters this case, then the channel completed
                    // TODO: should this throw an exception, as the channel is not supposed to complete during regular usage
                    break; 

                while (sub.Messages.TryRead(out var item))
                {
                    var broadcastSettingsUpdate = (BroadcastSettingsUpdateData)((NotificationData)item.Data).pubsub;

                    if (broadcastSettingsUpdate.old_game_id == broadcastSettingsUpdate.game_id)
                    {
                        continue;
                    }

                    var gameId = broadcastSettingsUpdate.game_id.ToString();
                    var gameInfo = (await TwitchHelper.GetGameInfo(gameId)).data.game;

                    chapters.Add((item.timestamp.ToUnixTimeMilliseconds(), gameInfo));
                }
            }

            chapters.Insert(0, await initialSettingsTask);
            return chapters;
        }

        private List<VideoChapter> ToVideoChapters(List<(long startMillisecondsUTC, TwitchObjects.Gql.GameData gameData)> protoChapters, long streamStartUTC, long streamEndUTC)
        {
            var streamLengthMs = (int)(streamEndUTC - streamStartUTC);

            var videoChapters = protoChapters
                // calculate start and end for each chapter in utc
                .Select((protoChapter, idx) =>
                {
                    var isLastChapter = idx == protoChapters.Count - 1;
                    var startMillisecondsUTC = Math.Max(streamStartUTC, protoChapter.startMillisecondsUTC); // the first chapter is 0, so math.max ensures proper utc
                    var endOfChapterUTC = isLastChapter ? streamEndUTC : protoChapters[idx + 1].startMillisecondsUTC; // only the first chapter does not have a proper startUTC, so this is safe
                    return (startMillisecondsUTC, endOfChapterUTC, protoChapter.gameData);
                })
                // remove chapers that are completely outside of the recording start and end
                .Where(protoChapter => protoChapter.endOfChapterUTC > streamStartUTC && protoChapter.startMillisecondsUTC < streamEndUTC)
                // turn chapter date into VideoChapter
                .Select(protoChapter => new VideoChapter
                {
                    id = "",
                    startMilliseconds = Math.Max(0, Math.Min((int)(protoChapter.startMillisecondsUTC - streamStartUTC), streamLengthMs)),
                    lengthMilliseconds = (int)(protoChapter.endOfChapterUTC - protoChapter.startMillisecondsUTC),
                    _type = "GAME_CHANGE",
                    description = protoChapter.gameData.displayName,
                    subDescription = "",
                    thumbnailUrl = null,
                    gameId = protoChapter.gameData.id,
                    gameDisplayName = protoChapter.gameData.displayName,
                    gameBoxArtUrl = protoChapter.gameData.boxArtURL
                })
                .ToList();

            // adjust the length of the last chapter so that it matches the total length
            var combinedChapterLengths = videoChapters.Aggregate(0, (sum, chapter) => sum + chapter.lengthMilliseconds);
            var chapterLengthMismatch = combinedChapterLengths - streamLengthMs;
            videoChapters.Last().lengthMilliseconds -= chapterLengthMismatch;

            return videoChapters;
        }

        private enum StreamStateChange { START, END }
        private async Task WaitFor(StreamStateChange target, TwitchEventHub eventHub, string streamerId, CancellationToken cancellationToken)
        {
            using var sub = await eventHub.SubscribeTo(streamerId, [TwitchEventHub.TwitchChatEvent.VideoPlaybackById]);

            var eventTarget = target switch { StreamStateChange.START => typeof(StreamUpData), StreamStateChange.END => typeof(StreamDownData), _ => throw new ArgumentException("invalid StreamStateChange", "target") };

            await foreach (var msg in sub.Messages.ReadAllAsync(cancellationToken))
            {
                if (eventTarget.IsInstanceOfType(((NotificationData)msg.Data).pubsub))
                {
                    break;
                }
            }
        }

        private Task GetEndOfRecordingSignal(TwitchEventHub eventHub, string streamerId, CancellationToken cancellationToken)
        {
            if (_recorderOptions.Duration.HasValue)
            {
                return Task.Delay(_recorderOptions.Duration.Value);
            }

            return WaitFor(StreamStateChange.END, eventHub, streamerId, cancellationToken);
        }
    }
}