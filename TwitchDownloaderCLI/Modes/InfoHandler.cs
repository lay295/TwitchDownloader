using Spectre.Console;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCLI.Modes
{
    internal static class InfoHandler
    {
        public static void PrintInfo(InfoArgs inputOptions)
        {
            using var progress = new CliTaskProgress(inputOptions.LogLevel);
            SetUtf8Encoding(inputOptions.UseUtf8.GetValueOrDefault(), progress);

            var vodClipIdMatch = IdParse.MatchVideoOrClipId(inputOptions.Id);
            if (vodClipIdMatch is not { Success: true })
            {
                progress.LogError("Unable to parse VOD/Clip ID/URL.");
                Environment.Exit(1);
            }

            inputOptions.Id = vodClipIdMatch.Value;
            if (inputOptions.Id.All(char.IsDigit))
            {
                HandleVod(inputOptions, progress);
            }
            else
            {
                HandleClip(inputOptions, progress);
            }
        }

        private static void HandleVod(InfoArgs inputOptions, ITaskProgress progress)
        {
            var videoId = long.Parse(inputOptions.Id);
            var (videoInfo, chapters, playlistString) = GetVideoInfo(videoId, inputOptions.Oauth, inputOptions.Format != InfoPrintFormat.Raw, progress).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case InfoPrintFormat.Raw:
                    HandleVodRaw(videoInfo, chapters, playlistString);
                    break;
                case InfoPrintFormat.Table:
                    HandleVodTable(videoInfo, chapters, playlistString);
                    break;
                case InfoPrintFormat.M3U8:
                    HandleVodM3U8(playlistString);
                    break;
                case InfoPrintFormat.Json:
                    HandleVodJson(videoInfo, chapters, playlistString);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<(GqlVideoResponse videoInfo, GqlVideoChapterResponse chapters, string playlistString)> GetVideoInfo(long videoId, string oauth, bool canThrow, ITaskProgress progress)
        {
            progress.SetStatus("Fetching Video Info [1/1]");

            var videoInfo = await TwitchHelper.GetVideoInfo(videoId);
            var accessToken = await TwitchHelper.GetVideoToken(videoId, oauth);

            if (accessToken.data.videoPlaybackAccessToken is null)
            {
                if (canThrow)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                return (videoInfo, null, null);
            }

            var playlistString = await TwitchHelper.GetVideoPlaylist(videoId, accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (canThrow && (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements")))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            var chapters = await TwitchHelper.GetOrGenerateVideoChapters(videoId, videoInfo.data.video, progress);

            return (videoInfo, chapters, playlistString);
        }

        private static void HandleVodRaw(GqlVideoResponse videoInfo, GqlVideoChapterResponse chapters, string playlistString)
        {
            var stdOut = Console.OpenStandardOutput();
            JsonSerializer.Serialize(stdOut, videoInfo);
            Console.WriteLine();
            JsonSerializer.Serialize(stdOut, chapters);
            Console.WriteLine();
            Console.Write(playlistString);
        }

        private static void HandleVodTable(GqlVideoResponse videoInfo, GqlVideoChapterResponse chapters, string playlistString)
        {
            const string DEFAULT_STRING = "-";
            var vodInfo = CreateVodInfo(videoInfo, chapters, playlistString);

            var infoTableTitle = new TableTitle("Video Info");
            var infoTable = new Table()
                .Title(infoTableTitle)
                .AddColumn(new TableColumn("Key"))
                .AddColumn(new TableColumn("Value"))
                .AddRow(new Markup("Streamer"), GetUserNameParagraph(vodInfo.VideoInfo.DisplayName, vodInfo.VideoInfo.Login, DEFAULT_STRING))
                .AddRow("Title", Markup.Escape(vodInfo.VideoInfo.Title))
                .AddRow("Length", StringifyTimestamp(vodInfo.VideoInfo.Length))
                .AddRow("Category", Markup.Escape(vodInfo.VideoInfo.Category))
                .AddRow("Views", vodInfo.VideoInfo.Views)
                .AddRow("Created at", StringifyCreatedAt(vodInfo.VideoInfo.CreatedAt))
                .AddRow("Description", Markup.Escape(vodInfo.VideoInfo.Description));

            AnsiConsole.Write(infoTable);

            var streamTableTitle = new TableTitle("Video Streams");
            var streamTable = new Table()
                .Title(streamTableTitle)
                .AddColumn(new TableColumn("Name"))
                .AddColumn(new TableColumn("Resolution"))
                .AddColumn(new TableColumn("FPS").RightAligned())
                .AddColumn(new TableColumn("Codecs").RightAligned());

            var hasBitrate = vodInfo.Qualities.Any(x => x.Bitrate is not null);
            if (hasBitrate)
            {
                streamTable
                    .AddColumn(new TableColumn("Bitrate").RightAligned())
                    .AddColumn(new TableColumn("File size").RightAligned());
            }

            foreach (var stream in vodInfo.Qualities)
            {
                if (hasBitrate)
                {
                    streamTable.AddRow(stream.Name, stream.Resolution, stream.Fps, stream.Codecs, stream.Bitrate, stream.FileSize);
                }
                else
                {
                    streamTable.AddRow(stream.Name, stream.Resolution, stream.Fps, stream.Codecs);
                }
            }

            AnsiConsole.Write(streamTable);

            if (vodInfo.Chapters.Length == 0)
                return;

            var chapterTableTitle = new TableTitle("Video Chapters");
            var chapterTable = new Table()
                .Title(chapterTableTitle)
                .AddColumn(new TableColumn("Category"))
                .AddColumn(new TableColumn("Type"))
                .AddColumn(new TableColumn("Start").RightAligned())
                .AddColumn(new TableColumn("End").RightAligned())
                .AddColumn(new TableColumn("Length").RightAligned());

            foreach (var chapter in vodInfo.Chapters)
            {
                chapterTable.AddRow(
                    Markup.Escape(chapter.Category),
                    chapter.Type,
                    StringifyTimestamp(chapter.Start),
                    StringifyTimestamp(chapter.End),
                    StringifyTimestamp(chapter.Length));
            }

            AnsiConsole.Write(chapterTable);
        }

        private static void HandleVodM3U8(string playlistString)
        {
            // Parse as m3u8 to verify that it is a valid playlist
            var m3u8 = M3U8.Parse(playlistString);
            Console.Write(m3u8.ToString());
        }

        private static void HandleVodJson(GqlVideoResponse videoInfo, GqlVideoChapterResponse chapters, string playlistString)
        {
            SerializeJson(CreateVodInfo(videoInfo, chapters, playlistString));
        }

        private static VodInfo CreateVodInfo(GqlVideoResponse videoInfo, GqlVideoChapterResponse chapters, string playlistString)
        {
            const string DEFAULT_STRING = "-";
            var infoVideo = videoInfo.data.video;
            var displayName = infoVideo.owner?.displayName;
            var login = infoVideo.owner?.login;
            var qualities = VideoQualities.FromM3U8(M3U8.Parse(playlistString));
            var hasBitrate = qualities.Any(x => x.BitRate != 0);
            var videoLength = TimeSpan.FromSeconds(infoVideo.lengthSeconds);

            var qualityInfos = qualities.Select(quality =>
            {
                var streamInfo = quality.Item.StreamInfo;
                return new VodQualityInfo(
                    quality.Name,
                    streamInfo.Resolution.StringifyOrDefault(x => x.ToString(), DEFAULT_STRING),
                    streamInfo.Framerate.StringifyOrDefault(x => $"{x:F0}", DEFAULT_STRING),
                    streamInfo.Codecs.StringifyOrDefault(x => string.Join(", ", x), DEFAULT_STRING),
                    hasBitrate ? quality.BitRate.StringifyOrDefault(x => $"{x / 1000}kbps", DEFAULT_STRING) : null,
                    hasBitrate ? quality.BitRate.StringifyOrDefault(x => $"~{VideoSizeEstimator.StringifyByteCount(VideoSizeEstimator.EstimateVideoSize(x, TimeSpan.Zero, videoLength))}", DEFAULT_STRING) : null);
            }).ToArray();

            var videoChapters = chapters.data.video.moments.edges.Select(chapter =>
            {
                var start = TimeSpan.FromMilliseconds(chapter.node.positionMilliseconds);
                var length = TimeSpan.FromMilliseconds(chapter.node.durationMilliseconds);
                return new VodChapterInfo(
                    chapter.node.details.game?.displayName ?? DEFAULT_STRING,
                    chapter.node._type,
                    start,
                    start + length,
                    length);
            }).ToArray();

            var videoDetails = new VideoInfo(
                FormatUser(displayName, login, DEFAULT_STRING),
                infoVideo.title,
                videoLength,
                infoVideo.game?.displayName ?? DEFAULT_STRING,
                infoVideo.viewCount.ToString("N0", CultureInfo.CurrentCulture),
                infoVideo.createdAt,
                infoVideo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd() ?? DEFAULT_STRING,
                displayName,
                login);

            return new VodInfo(videoDetails, qualityInfos, videoChapters);
        }

        private static void HandleClip(InfoArgs inputOptions, ITaskProgress progress)
        {
            var clipRenderStatus = GetClipInfo(inputOptions.Id, inputOptions.Format != InfoPrintFormat.Raw, progress).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case InfoPrintFormat.Raw:
                    HandleClipRaw(clipRenderStatus);
                    break;
                case InfoPrintFormat.Table:
                    HandleClipTable(clipRenderStatus);
                    break;
                case InfoPrintFormat.M3U8:
                    HandleClipM3U8(clipRenderStatus);
                    break;
                case InfoPrintFormat.Json:
                    HandleClipJson(clipRenderStatus);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<GqlShareClipRenderStatusResponse> GetClipInfo(string clipId, bool canThrow, ITaskProgress progress)
        {
            progress.SetStatus("Fetching Clip Info [1/1]");

            var clipRenderStatus = await TwitchHelper.GetShareClipRenderStatus(clipId);

            if (!canThrow)
            {
                return clipRenderStatus;
            }

            var clip = clipRenderStatus.data.clip;
            if (clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (clip.assets is not { Length: > 0 } || clip.assets[0].videoQualities is not { Length: > 0 })
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            return clipRenderStatus;
        }

        private static void HandleClipRaw(GqlShareClipRenderStatusResponse clipRenderStatus)
        {
            var stdOut = Console.OpenStandardOutput();
            JsonSerializer.Serialize(stdOut, clipRenderStatus);
        }

        private static void HandleClipTable(GqlShareClipRenderStatusResponse clipRenderStatus)
        {
            const string DEFAULT_STRING = "-";
            var clipInfo = CreateClipInfo(clipRenderStatus);

            var infoTableTitle = new TableTitle("Clip Info");
            var infoTable = new Table()
                .Title(infoTableTitle)
                .AddColumn(new TableColumn("Key"))
                .AddColumn(new TableColumn("Value"))
                .AddRow(new Markup("Streamer"), GetUserNameParagraph(clipInfo.VideoInfo.DisplayName, clipInfo.VideoInfo.Login, DEFAULT_STRING))
                .AddRow("Title", Markup.Escape(clipInfo.VideoInfo.Title))
                .AddRow("Length", StringifyTimestamp(clipInfo.VideoInfo.Length))
                .AddRow(new Markup("Clipped by"), GetUserNameParagraph(clipInfo.VideoInfo.ClippedByDisplayName, clipInfo.VideoInfo.ClippedByLogin, DEFAULT_STRING))
                .AddRow("Category", Markup.Escape(clipInfo.VideoInfo.Category))
                .AddRow("Views", clipInfo.VideoInfo.Views)
                .AddRow("Created at", StringifyCreatedAt(clipInfo.VideoInfo.CreatedAt));

            if (clipInfo.VideoInfo.VodId is not null)
            {
                infoTable
                    .AddRow("VOD ID", clipInfo.VideoInfo.VodId)
                    .AddRow("VOD offset", StringifyTimestamp(clipInfo.VideoInfo.VodOffset.Value));
            }

            AnsiConsole.Write(infoTable);

            var qualityTableTitle = new TableTitle("Clip Qualities");
            var qualityTable = new Table()
                .Title(qualityTableTitle)
                .AddColumn(new TableColumn("Name"))
                .AddColumn(new TableColumn("Resolution"))
                .AddColumn(new TableColumn("FPS").RightAligned());

            var hasBitrate = clipInfo.Qualities.Any(x => x.Bitrate is not null);
            if (hasBitrate)
            {
                qualityTable
                    .AddColumn(new TableColumn("Bitrate").RightAligned())
                    .AddColumn(new TableColumn("File size").RightAligned());
            }

            foreach (var quality in clipInfo.Qualities)
            {
                if (hasBitrate)
                {
                    qualityTable.AddRow(quality.Name, quality.Resolution, quality.Fps, quality.Bitrate, quality.FileSize);
                }
                else
                {
                    qualityTable.AddRow(quality.Name, quality.Resolution, quality.Fps);
                }
            }

            AnsiConsole.Write(qualityTable);
        }

        private static void HandleClipM3U8(GqlShareClipRenderStatusResponse clipRenderStatus)
        {
            var clip = clipRenderStatus.data.clip;
            var clipQualities = VideoQualities.FromClip(clip);

            var metadata = new M3U8.Metadata
            {
                Version = null,
                MediaSequence = 0,
                StreamTargetDuration = (uint)clip.durationSeconds,
                TwitchElapsedSeconds = 0,
                TwitchLiveSequence = null,
                TwitchTotalSeconds = clip.durationSeconds,
                Type = M3U8.Metadata.PlaylistType.Event,
            };

            var streams = clipQualities.Qualities
                .Select(x =>
                {
                    string[] ivsGroups = x.Orientation switch
                    {
                        VideoOrientation.Landscape => ["landscape"],
                        VideoOrientation.Portrait => ["portrait"],
                        _ => []
                    };

                    return new M3U8.Stream(
                        new M3U8.Stream.ExtStreamInfo(0, x.BitRate, x.Item.codecs?.Split(','), x.Resolution, x.Framerate, x.Item.quality, x.Item.quality, ivsGroups, "source"),
                        $"{x.Item.sourceURL}?sig={clip.playbackAccessToken.signature}&token={HttpUtility.UrlEncode(clip.playbackAccessToken.value)}"
                    );
                })
                .ToArray();

            var m3u8 = new M3U8(metadata, streams);
            Console.Write(m3u8.ToString());
        }

        private static void HandleClipJson(GqlShareClipRenderStatusResponse clipRenderStatus)
        {
            SerializeJson(CreateClipInfo(clipRenderStatus));
        }

        private static ClipInfo CreateClipInfo(GqlShareClipRenderStatusResponse clipRenderStatus)
        {
            const string DEFAULT_STRING = "-";
            var infoClip = clipRenderStatus.data.clip;
            var qualities = VideoQualities.FromClip(infoClip);
            var hasBitrate = qualities.Any(x => x.BitRate != 0);
            var videoLength = TimeSpan.FromSeconds(infoClip.durationSeconds);
            var displayName = infoClip.broadcaster?.displayName;
            var login = infoClip.broadcaster?.login;
            var clippedByDisplayName = infoClip.curator?.displayName;
            var clippedByLogin = infoClip.curator?.login;

            var clipQualities = qualities.Qualities.Select(quality => new ClipQualityInfo(
                quality.Name,
                quality.Resolution.HasWidth ? quality.Resolution.ToString() : quality.Resolution.Height.ToString(),
                quality.Framerate.StringifyOrDefault(x => $"{x:F0}", DEFAULT_STRING),
                hasBitrate ? quality.BitRate.StringifyOrDefault(x => $"{x / 1000}kbps", DEFAULT_STRING) : null,
                hasBitrate ? quality.BitRate.StringifyOrDefault(x => $"~{VideoSizeEstimator.StringifyByteCount(VideoSizeEstimator.EstimateVideoSize(x, TimeSpan.Zero, videoLength))}", DEFAULT_STRING) : null))
                .ToArray();

            var videoInfo = new ClipVideoInfo(
                FormatUser(displayName, login, DEFAULT_STRING),
                infoClip.title,
                videoLength,
                FormatUser(clippedByDisplayName, clippedByLogin, DEFAULT_STRING),
                infoClip.game?.displayName ?? DEFAULT_STRING,
                infoClip.viewCount.ToString("N0", CultureInfo.CurrentCulture),
                infoClip.createdAt,
                infoClip.video?.id,
                infoClip.video is null || infoClip.videoOffsetSeconds is null ? null : TimeSpan.FromSeconds(infoClip.videoOffsetSeconds.Value),
                displayName,
                login,
                clippedByDisplayName,
                clippedByLogin);

            return new ClipInfo(videoInfo, clipQualities);
        }

        private static string StringifyOrDefault<T>(this T value, Func<T, string> stringify, string defaultString) where T : IEquatable<T>
        {
            if (!typeof(T).IsValueType && value is null)
            {
                return defaultString;
            }

            if (!value.Equals(default))
            {
                return stringify(value);
            }

            return defaultString;
        }

        private static string StringifyOrDefault<T>(this T? value, Func<T, string> stringify, string defaultString) where T : struct, IEquatable<T>
        {
            if (value.HasValue)
            {
                return stringify(value.Value);
            }

            return defaultString;
        }

        private static string StringifyOrDefault<T>([AllowNull] this IEnumerable<T> values, Func<IEnumerable<T>, string> stringify, string defaultString)
        {
            if (values is not null && values.Any())
            {
                return stringify(values);
            }

            return defaultString;
        }

        private static string StringifyTimestamp(TimeSpan timeSpan)
        {
            return timeSpan.Ticks switch
            {
                < TimeSpan.TicksPerSecond => "0:00",
                < TimeSpan.TicksPerMinute => timeSpan.ToString(@"s\s"),
                < TimeSpan.TicksPerHour => timeSpan.ToString(@"m\:ss"),
                _ => TimeSpanHFormat.ReusableInstance.Format(@"H\:mm\:ss", timeSpan)
            };
        }

        private static string StringifyCreatedAt(DateTimeOffset timestamp)
            => $"{timestamp.ToUniversalTime():yyyy-MM-dd hh:mm:ss} UTC";

        private static string FormatUser(string displayName, string login, string defaultName)
            => string.IsNullOrWhiteSpace(displayName)
                ? (string.IsNullOrWhiteSpace(login) ? defaultName : login)
                : string.IsNullOrWhiteSpace(login) || displayName.All(char.IsAscii)
                    ? displayName
                    : $"{displayName} ({login})";

        private static void SerializeJson<T>(T value)
            => JsonSerializer.Serialize(Console.OpenStandardOutput(), value, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new InfoTimeSpanConverter(), new InfoDateTimeOffsetConverter() },
                });

        private sealed record VodInfo(VideoInfo VideoInfo, VodQualityInfo[] Qualities, VodChapterInfo[] Chapters);

        private sealed record VideoInfo(
            string Streamer,
            string Title,
            TimeSpan Length,
            string Category,
            string Views,
            DateTimeOffset CreatedAt,
            string Description,
            [property: JsonIgnore] string DisplayName,
            [property: JsonIgnore] string Login);

        private sealed record VodQualityInfo(string Name, string Resolution, string Fps, string Codecs, string Bitrate, string FileSize);

        private sealed record VodChapterInfo(string Category, string Type, TimeSpan Start, TimeSpan End, TimeSpan Length);

        private sealed record ClipInfo(ClipVideoInfo VideoInfo, ClipQualityInfo[] Qualities);

        private sealed record ClipVideoInfo(
            string Streamer,
            string Title,
            TimeSpan Length,
            string ClippedBy,
            string Category,
            string Views,
            DateTimeOffset CreatedAt,
            string VodId,
            TimeSpan? VodOffset,
            [property: JsonIgnore] string DisplayName,
            [property: JsonIgnore] string Login,
            [property: JsonIgnore] string ClippedByDisplayName,
            [property: JsonIgnore] string ClippedByLogin);

        private sealed record ClipQualityInfo(string Name, string Resolution, string Fps, string Bitrate, string FileSize);

        private sealed class InfoTimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
                writer.WriteStringValue(StringifyTimestamp(value));
        }

        private sealed class InfoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
        {
            public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException();

            public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
                writer.WriteStringValue(StringifyCreatedAt(value));
        }

        private static Paragraph GetUserNameParagraph([AllowNull] string displayName, [AllowNull] string login, string @default)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return string.IsNullOrWhiteSpace(login) ? new Paragraph(@default) : new Paragraph(login, link: new Link($"https://twitch.tv/{login}"));
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                return new Paragraph(displayName);
            }

            if (displayName.All(char.IsAscii))
            {
                return new Paragraph(displayName, link: new Link($"https://twitch.tv/{login}"));
            }

            return new Paragraph($"{displayName} ({login})", link: new Link($"https://twitch.tv/{login}"));
        }

        // cmd.exe only supports chars from codepage 437, so the default console encoding on Windows is codepage 437 instead of UTF-8
        private static void SetUtf8Encoding(bool useUtf8, ITaskLogger logger)
        {
            if (!useUtf8 || Console.OutputEncoding.CodePage == Encoding.UTF8.CodePage)
            {
                return;
            }

            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                logger.LogVerbose("Output encoding has switched to UTF-8.");
            }
            catch
            {
                logger.LogWarning("Failed to set UTF-8 encoding. Non-ASCII characters may not render correctly.");
            }
        }
    }
}