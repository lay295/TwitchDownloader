using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Spectre.Console;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCLI.Modes
{
    internal static class InfoHandler
    {
        public static void PrintInfo(InfoArgs inputOptions)
        {
            var progress = new CliTaskProgress(inputOptions.LogLevel);
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
                    HandleVodJson();
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

            var chapters = await TwitchHelper.GetOrGenerateVideoChapters(videoId, videoInfo.data.video);

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
            var m3u8 = M3U8.Parse(playlistString);
            m3u8.SortStreamsByQuality();

            const string DEFAULT_STRING = "-";
            var infoVideo = videoInfo.data.video;

            var infoTableTitle = new TableTitle("Video Info");
            var infoTable = new Table()
                .Title(infoTableTitle)
                .AddColumn(new TableColumn("Key"))
                .AddColumn(new TableColumn("Value"))
                .AddRow(new Markup("Streamer"), GetUserNameMarkup(infoVideo.owner?.displayName, infoVideo.owner?.login, DEFAULT_STRING))
                .AddRow(new Markup("Title"), new Paragraph(infoVideo.title))
                .AddRow("Length", StringifyTimestamp(TimeSpan.FromSeconds(infoVideo.lengthSeconds)))
                .AddRow("Category", infoVideo.game?.displayName ?? DEFAULT_STRING)
                .AddRow("Views", infoVideo.viewCount.ToString("N0", CultureInfo.CurrentCulture))
                .AddRow("Created at", $"{infoVideo.createdAt.ToUniversalTime():yyyy-MM-dd hh:mm:ss} UTC")
                .AddRow("Description", infoVideo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd() ?? DEFAULT_STRING);

            AnsiConsole.Write(infoTable);

            var streamTableTitle = new TableTitle("Video Streams");
            var streamTable = new Table()
                .Title(streamTableTitle)
                .AddColumn(new TableColumn("Name"))
                .AddColumn(new TableColumn("Resolution"))
                .AddColumn(new TableColumn("FPS").RightAligned())
                .AddColumn(new TableColumn("Codecs").RightAligned());

            var hasBitrate = m3u8.Streams.Any(x => x.StreamInfo.Bandwidth != default);
            if (hasBitrate)
            {
                streamTable
                    .AddColumn(new TableColumn("Bitrate").RightAligned())
                    .AddColumn(new TableColumn("File size").RightAligned());
            }

            foreach (var stream in m3u8.Streams)
            {
                var name = stream.GetResolutionFramerateString();
                var resolution = stream.StreamInfo.Resolution.StringifyOrDefault(x => x.ToString(), DEFAULT_STRING);
                var fps = stream.StreamInfo.Framerate.StringifyOrDefault(x => $"{x:F0}", DEFAULT_STRING);
                var codecs = stream.StreamInfo.Codecs.StringifyOrDefault(x => string.Join(", ", x), DEFAULT_STRING);

                if (hasBitrate)
                {
                    var videoLength = TimeSpan.FromSeconds(infoVideo.lengthSeconds);
                    var bitrate = stream.StreamInfo.Bandwidth.StringifyOrDefault(x => $"{x / 1000}kbps", DEFAULT_STRING);
                    var fileSize = stream.StreamInfo.Bandwidth.StringifyOrDefault(x => $"~{VideoSizeEstimator.StringifyByteCount(VideoSizeEstimator.EstimateVideoSize(x, TimeSpan.Zero, videoLength))}", DEFAULT_STRING);
                    streamTable.AddRow(name, resolution, fps, codecs, bitrate, fileSize);
                }
                else
                {
                    streamTable.AddRow(name, resolution, fps, codecs);
                }
            }

            AnsiConsole.Write(streamTable);

            if (chapters.data.video.moments.edges.Count == 0)
                return;

            var chapterTableTitle = new TableTitle("Video Chapters");
            var chapterTable = new Table()
                .Title(chapterTableTitle)
                .AddColumn(new TableColumn("Category"))
                .AddColumn(new TableColumn("Type"))
                .AddColumn(new TableColumn("Start").RightAligned())
                .AddColumn(new TableColumn("End").RightAligned())
                .AddColumn(new TableColumn("Length").RightAligned());

            foreach (var chapter in chapters.data.video.moments.edges)
            {
                var category = chapter.node.details.game?.displayName ?? DEFAULT_STRING;
                var type = chapter.node._type;
                var start = TimeSpan.FromMilliseconds(chapter.node.positionMilliseconds);
                var length = TimeSpan.FromMilliseconds(chapter.node.durationMilliseconds);
                var end = start + length;
                var startString = StringifyTimestamp(start);
                var endString = StringifyTimestamp(end);
                var lengthString = StringifyTimestamp(length);
                chapterTable.AddRow(category, type, startString, endString, lengthString);
            }

            AnsiConsole.Write(chapterTable);
        }

        private static void HandleVodM3U8(string playlistString)
        {
            // Parse as m3u8 to verify that it is a valid playlist
            var m3u8 = M3U8.Parse(playlistString);
            Console.Write(m3u8.ToString());
        }

        private static void HandleVodJson()
        {
            throw new NotImplementedException("JSON format is not yet supported");
        }

        private static void HandleClip(InfoArgs inputOptions, ITaskProgress progress)
        {
            var (clipInfo, clipQualities) = GetClipInfo(inputOptions.Id, inputOptions.Format != InfoPrintFormat.Raw, progress).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case InfoPrintFormat.Raw:
                    HandleClipRaw(clipInfo, clipQualities);
                    break;
                case InfoPrintFormat.Table:
                    HandleClipTable(clipInfo, clipQualities);
                    break;
                case InfoPrintFormat.M3U8:
                    HandleClipM3U8(clipQualities, clipInfo);
                    break;
                case InfoPrintFormat.Json:
                    HandleClipJson();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<(GqlClipResponse clipInfo, GqlClipTokenResponse listLinks)> GetClipInfo(string clipId, bool canThrow, ITaskProgress progress)
        {
            progress.SetStatus("Fetching Clip Info [1/1]");

            var clipInfo = await TwitchHelper.GetClipInfo(clipId);
            var listLinks = await TwitchHelper.GetClipLinks(clipId);

            if (!canThrow)
            {
                return (clipInfo, listLinks);
            }

            var clip = listLinks.data.clip;
            if (clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (clip.videoQualities is null || clip.videoQualities.Length == 0)
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            return (clipInfo, listLinks);
        }

        private static void HandleClipRaw(GqlClipResponse clipInfo, GqlClipTokenResponse clipQualities)
        {
            var stdOut = Console.OpenStandardOutput();
            JsonSerializer.Serialize(stdOut, clipInfo);
            Console.WriteLine();
            JsonSerializer.Serialize(stdOut, clipQualities);
        }

        private static void HandleClipTable(GqlClipResponse clipInfo, GqlClipTokenResponse clipQualities)
        {
            const string DEFAULT_STRING = "-";
            var infoClip = clipInfo.data.clip;

            var infoTableTitle = new TableTitle("Clip Info");
            var infoTable = new Table()
                .Title(infoTableTitle)
                .AddColumn(new TableColumn("Key"))
                .AddColumn(new TableColumn("Value"))
                .AddRow(new Markup("Streamer"), GetUserNameMarkup(infoClip.broadcaster?.displayName, infoClip.broadcaster?.login, DEFAULT_STRING))
                .AddRow(new Markup("Title"), new Paragraph(infoClip.title))
                .AddRow("Length", StringifyTimestamp(TimeSpan.FromSeconds(infoClip.durationSeconds)))
                .AddRow(new Markup("Clipped by"), GetUserNameMarkup(infoClip.curator?.displayName, infoClip.curator?.login, DEFAULT_STRING))
                .AddRow("Category", infoClip.game?.displayName ?? DEFAULT_STRING)
                .AddRow("Views", infoClip.viewCount.ToString("N0", CultureInfo.CurrentCulture))
                .AddRow("Created at", $"{infoClip.createdAt.ToUniversalTime():yyyy-MM-dd hh:mm:ss} UTC");

            if (infoClip.video != null)
            {
                var videoOffset = infoClip.videoOffsetSeconds.StringifyOrDefault(x => StringifyTimestamp(TimeSpan.FromSeconds(x)), DEFAULT_STRING);
                infoTable
                    .AddRow("VOD ID", infoClip.video.id)
                    .AddRow("VOD offset", videoOffset);
            }

            AnsiConsole.Write(infoTable);

            var qualityTableTitle = new TableTitle("Clip Qualities");
            var qualityTable = new Table()
                .Title(qualityTableTitle)
                .AddColumn(new TableColumn("Name"))
                .AddColumn(new TableColumn("Height"))
                .AddColumn(new TableColumn("FPS").RightAligned());

            foreach (var quality in clipQualities.data.clip.videoQualities)
            {
                var name = string.Create(CultureInfo.CurrentCulture, $"{quality.quality}p{quality.frameRate:F0}");
                var height = quality.quality;
                var fps = quality.frameRate.StringifyOrDefault(x => $"{x:F0}", DEFAULT_STRING);
                qualityTable.AddRow(name, height, fps);
            }

            AnsiConsole.Write(qualityTable);
        }

        private static void HandleClipM3U8(GqlClipTokenResponse clipQualities, GqlClipResponse clipInfo)
        {
            var clip = clipQualities.data.clip;

            var metadata = new M3U8.Metadata
            {
                Version = default,
                MediaSequence = 0,
                StreamTargetDuration = (uint)clipInfo.data.clip.durationSeconds,
                TwitchElapsedSeconds = 0,
                TwitchLiveSequence = default,
                TwitchTotalSeconds = clipInfo.data.clip.durationSeconds,
                Type = M3U8.Metadata.PlaylistType.Event,
            };

            var streams = clip.videoQualities.Select(x => new M3U8.Stream(
                new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, x.quality, x.quality, true, true),
                new M3U8.Stream.ExtStreamInfo(default, default, default, default, x.quality, x.frameRate),
                $"{x.sourceURL}?sig={clip.playbackAccessToken.signature}&token={HttpUtility.UrlEncode(clip.playbackAccessToken.value)}"
            )).ToArray();

            var m3u8 = new M3U8(metadata, streams);
            Console.Write(m3u8.ToString());
        }

        private static void HandleClipJson()
        {
            throw new NotImplementedException("JSON format is not yet supported");
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

        private static Markup GetUserNameMarkup([AllowNull] string displayName, [AllowNull] string login, string @default)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return string.IsNullOrWhiteSpace(login) ? new Markup(@default) : new Markup(login, Style.Plain.Link($"https://twitch.tv/{login}"));
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                return new Markup(displayName);
            }

            if (displayName.All(char.IsAscii))
            {
                return new Markup(displayName, Style.Plain.Link($"https://twitch.tv/{login}"));
            }

            return new Markup($"{displayName} ({login})", Style.Plain.Link($"https://twitch.tv/{login}"));
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