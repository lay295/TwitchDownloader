using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Services
{
    internal sealed class MassEnqueueContext
    {
        public required AppSettings Settings { get; init; }
        public required string FfmpegPath { get; init; }
        public required Func<FileInfo, FileInfo> CollisionCallback { get; init; }
        public required LogLevel LogLevel { get; init; }
    }

    internal static class MassEnqueuePlanner
    {
        public static IReadOnlyList<QueueItemViewModel> Build(
            IReadOnlyList<QueueableMedia> items,
            EnqueueOptions options,
            MassEnqueueContext context)
        {
            if (options is { DownloadVideo: false, DownloadChat: false })
                return [];

            var settings = context.Settings;
            var throttle = settings.DownloadThrottleEnabled ? settings.MaximumBandwidthKib : -1;
            var tasks = new List<QueueItemViewModel>(items.Count * 3);
            var renderChat = options is { RenderChat: true, DownloadChat: true, ChatFormat: ChatFormat.Json };
            var chatCompression = options is { ChatFormat: ChatFormat.Json, ChatCompression: ChatCompression.Gzip }
                ? ChatCompression.Gzip
                : ChatCompression.None;

            var container = renderChat ? ChatRenderOptionsFactory.ContainerExtension(settings) : string.Empty;

            foreach (var item in items)
            {
                string? videoFilename = null;
                if (options.DownloadVideo)
                {
                    if (item.IsClip)
                    {
                        var clipOptions = new ClipDownloadOptions
                        {
                            Id = item.Id,
                            Quality = options.Quality,
                            Filename = Path.Combine(options.Folder, FilenameService.GetFilename(
                                settings.TemplateClip,
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
                                NullIfEmpty(item.ClipperName),
                                NullIfEmpty(item.ClipperId)) + ".mp4"),
                            ThrottleKib = throttle,
                            TempFolder = settings.TempPath,
                            EncodeMetadata = settings.EncodeClipMetadata,
                            FfmpegPath = context.FfmpegPath,
                            FileCollisionCallback = context.CollisionCallback,
                        };

                        videoFilename = clipOptions.Filename;
                        tasks.Add(QueueItemViewModel.CreateClip(clipOptions, item.Title, item.ThumbnailBytes, context.LogLevel));
                    }
                    else if (long.TryParse(item.Id, out var videoId))
                    {
                        var vodOptions = new VideoDownloadOptions
                        {
                            Oauth = settings.OAuth,
                            TempFolder = settings.TempPath,
                            Id = videoId,
                            Quality = options.Quality,
                            FfmpegPath = context.FfmpegPath,
                            TrimBeginning = false,
                            TrimEnding = false,
                            DownloadThreads = Math.Clamp(settings.VodDownloadThreads, 1, 20),
                            ThrottleKib = throttle,
                            FileCollisionCallback = context.CollisionCallback,
                            CacheCleanerCallback = _ => [],
                            DelayDownload = options.DelayVideo && item.IsRecording,
                        };

                        vodOptions.Filename = Path.Combine(options.Folder, FilenameService.GetFilename(
                            settings.TemplateVod,
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

                        videoFilename = vodOptions.Filename;
                        tasks.Add(QueueItemViewModel.CreateVod(vodOptions, item.Title, item.ThumbnailBytes, context.LogLevel));
                    }
                }

                if (!options.DownloadChat)
                    continue;

                var chatOptions = new ChatDownloadOptions
                {
                    EmbedData = options.EmbedImages,
                    BttvEmotes = options.Bttv,
                    FfzEmotes = options.Ffz,
                    StvEmotes = options.Stv,
                    TimeFormat = settings.ChatTextTimestampStyle,
                    Id = item.Id,
                    TrimBeginning = false,
                    TrimEnding = false,
                    FileCollisionCallback = context.CollisionCallback,
                    DownloadFormat = options.ChatFormat,
                    Compression = chatCompression,
                    DownloadThreads = Math.Clamp(settings.ChatDownloadThreads, 1, 20),
                    TempFolder = settings.TempPath,
                    DelayDownload = item.IsRecording && options.DelayChat,
                };

                chatOptions.Filename = Path.Combine(options.Folder, FilenameService.GetFilename(
                    settings.TemplateChat,
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
                    NullIfEmpty(item.ClipperName),
                    NullIfEmpty(item.ClipperId)) + chatOptions.FileExtension);

                var chatItem = QueueItemViewModel.CreateChat(chatOptions, item.Title, item.ThumbnailBytes, context.LogLevel);
                tasks.Add(chatItem);

                if (!renderChat)
                    continue;

                var renderOutput = MassEnqueuePaths.ChatRenderOutput(chatOptions.Filename, videoFilename, container);
                var renderOptions = ChatRenderOptionsFactory.FromSettings(
                    settings,
                    chatOptions.Filename,
                    renderOutput,
                    context.FfmpegPath,
                    context.CollisionCallback);

                tasks.Add(QueueItemViewModel.CreateChatRender(
                    renderOptions,
                    item.Title,
                    item.ThumbnailBytes,
                    context.LogLevel,
                    chatItem));
            }

            return tasks;
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
