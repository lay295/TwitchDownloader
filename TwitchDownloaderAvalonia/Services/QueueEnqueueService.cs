using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class QueueEnqueueService(
        SettingsService settings,
        DialogService dialogs,
        QueueService queue,
        FfmpegService ffmpeg,
        FileCollisionService collision)
    {
        public async Task<bool> EnqueueAsync(IReadOnlyList<QueueableMedia> items)
        {
            if (items.Count == 0)
                return false;

            var options = await dialogs.ShowEnqueueOptionsAsync(items.Any(item => !item.IsClip));
            if (options is null)
                return false;

            try
            {
                if (!Directory.Exists(options.Folder))
                    TwitchHelper.CreateDirectory(options.Folder);
            }
            catch (Exception ex)
            {
                await dialogs.ShowErrorAsync(Loc.Get("search.invalid_folder_title"), Loc.Get("search.invalid_folder"));
                if (settings.Current.VerboseErrors)
                    await dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());

                return false;
            }

            var collisionCallback = collision.HandleCollision;
            var throttle = settings.Current.DownloadThrottleEnabled ? settings.Current.MaximumBandwidthKib : -1;
            var tasks = new List<QueueItemViewModel>(items.Count * (options.DownloadChat ? 2 : 1));

            foreach (var item in items)
            {
                if (item.IsClip)
                {
                    var clipOptions = new ClipDownloadOptions
                    {
                        Id = item.Id,
                        Quality = options.Quality,
                        Filename = Path.Combine(options.Folder, FilenameService.GetFilename(
                            settings.Current.TemplateClip,
                            item.Title,
                            item.Id,
                            item.Time,
                            item.StreamerName,
                            item.StreamerId,
                            TimeSpan.Zero,
                            TimeSpan.FromSeconds(item.Length),
                            TimeSpan.FromSeconds(item.Length),
                            item.Views,
                            item.Game,
                            string.IsNullOrEmpty(item.ClipperName) ? null : item.ClipperName,
                            string.IsNullOrEmpty(item.ClipperId) ? null : item.ClipperId) + ".mp4"),
                        ThrottleKib = throttle,
                        TempFolder = settings.Current.TempPath,
                        EncodeMetadata = settings.Current.EncodeClipMetadata,
                        FfmpegPath = ffmpeg.ResolvedPath,
                        FileCollisionCallback = collisionCallback,
                    };

                    tasks.Add(QueueItemViewModel.CreateClip(clipOptions, item.Title, item.ThumbnailBytes, queue.LogLevel));
                }
                else if (long.TryParse(item.Id, out var videoId))
                {
                    var vodOptions = new VideoDownloadOptions
                    {
                        Oauth = settings.Current.OAuth,
                        TempFolder = settings.Current.TempPath,
                        Id = videoId,
                        Quality = options.Quality,
                        FfmpegPath = ffmpeg.ResolvedPath,
                        TrimBeginning = false,
                        TrimEnding = false,
                        DownloadThreads = Math.Clamp(settings.Current.VodDownloadThreads, 1, 20),
                        ThrottleKib = throttle,
                        FileCollisionCallback = collisionCallback,
                        CacheCleanerCallback = _ => [],
                    };

                    vodOptions.Filename = Path.Combine(options.Folder, FilenameService.GetFilename(
                        settings.Current.TemplateVod,
                        item.Title,
                        item.Id,
                        item.Time,
                        item.StreamerName,
                        item.StreamerId,
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(item.Length),
                        TimeSpan.FromSeconds(item.Length),
                        item.Views,
                        item.Game) + FilenameService.GuessVodFileExtension(vodOptions.Quality));

                    tasks.Add(QueueItemViewModel.CreateVod(vodOptions, item.Title, item.ThumbnailBytes, queue.LogLevel));
                }

                if (!options.DownloadChat)
                    continue;

                var chatOptions = new ChatDownloadOptions
                {
                    EmbedData = settings.Current.ChatEmbedEmotes,
                    BttvEmotes = settings.Current.BttvEmotes,
                    FfzEmotes = settings.Current.FfzEmotes,
                    StvEmotes = settings.Current.StvEmotes,
                    TimeFormat = settings.Current.ChatTextTimestampStyle,
                    Id = item.Id,
                    TrimBeginning = false,
                    TrimEnding = false,
                    FileCollisionCallback = collisionCallback,
                    DownloadFormat = settings.Current.ChatDownloadFormat,
                    Compression = settings.Current.ChatDownloadFormat == ChatFormat.Json
                        ? settings.Current.ChatJsonCompression
                        : ChatCompression.None,
                    DownloadThreads = Math.Clamp(settings.Current.ChatDownloadThreads, 1, 20),
                    TempFolder = settings.Current.TempPath,
                };

                chatOptions.Filename = Path.Combine(options.Folder, FilenameService.GetFilename(
                    settings.Current.TemplateChat,
                    item.Title,
                    item.Id,
                    item.Time,
                    item.StreamerName,
                    item.StreamerId,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(item.Length),
                    TimeSpan.FromSeconds(item.Length),
                    item.Views,
                    item.Game,
                    string.IsNullOrEmpty(item.ClipperName) ? null : item.ClipperName,
                    string.IsNullOrEmpty(item.ClipperId) ? null : item.ClipperId) + chatOptions.FileExtension);

                tasks.Add(QueueItemViewModel.CreateChat(chatOptions, item.Title, item.ThumbnailBytes, queue.LogLevel));
            }

            if (tasks.Count == 0)
                return false;

            queue.EnqueueRange(tasks);
            return true;
        }
    }
}
