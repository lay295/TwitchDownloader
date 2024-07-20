using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
    internal static class StreamInfo
    {
        public static void PrintInfo(StreamInfoArgs inputOptions)
        {
            var progress = new CliTaskProgress(inputOptions.LogLevel);

            var vodClipIdMatch = TwitchRegex.MatchVideoOrClipId(inputOptions.Id);
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

        private static void HandleVod(StreamInfoArgs inputOptions, CliTaskProgress progress)
        {
            var videoId = long.Parse(inputOptions.Id);
            var (videoInfo, playlistString) = GetPlaylistInfo(videoId, inputOptions.Oauth, inputOptions.Format != StreamInfoPrintFormat.Raw, progress).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case StreamInfoPrintFormat.Raw:
                {
                    var stdOut = Console.OpenStandardOutput();
                    JsonSerializer.Serialize(stdOut, videoInfo);
                    Console.WriteLine();
                    Console.Write(playlistString);
                    break;
                }
                case StreamInfoPrintFormat.Table:
                {
                    var m3u8 = M3U8.Parse(playlistString);
                    m3u8.SortStreamsByQuality();

                    const string DEFAULT_STRING = "-";
                    var infoVideo = videoInfo.data.video;
                    var hasBitrate = m3u8.Streams.Any(x => x.StreamInfo.Bandwidth != default);

                    var streamTableTitle = new TableTitle($"{infoVideo.owner.displayName} - {infoVideo.title}", Style.Plain.Link($"https://twitch.tv/videos/{inputOptions.Id}"));
                    var streamTable = new Table()
                        .Title(streamTableTitle)
                        .AddColumn(new TableColumn("Name"))
                        .AddColumn(new TableColumn("Resolution"))
                        .AddColumn(new TableColumn("FPS").RightAligned())
                        .AddColumn(new TableColumn("Codecs").RightAligned());

                    if (hasBitrate)
                    {
                        streamTable
                            .AddColumn(new TableColumn("File size").RightAligned())
                            .AddColumn(new TableColumn("Bitrate").RightAligned());
                    }

                    foreach (var stream in m3u8.Streams)
                    {
                        var name = stream.GetResolutionFramerateString();
                        var resolution = stream.StreamInfo.Resolution.StringifyOrDefault(x => x.ToString(), DEFAULT_STRING);
                        var fps = stream.StreamInfo.Framerate.StringifyOrDefault(x => x.ToString(CultureInfo.CurrentCulture), DEFAULT_STRING);
                        var codecs = stream.StreamInfo.Codecs.StringifyOrDefault(x => x, DEFAULT_STRING);

                        if (hasBitrate)
                        {
                            var videoLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                            var fileSize = stream.StreamInfo.Bandwidth.StringifyOrDefault(x => $"~{VideoSizeEstimator.StringifyByteCount(VideoSizeEstimator.EstimateVideoSize(x, TimeSpan.Zero, videoLength))}", DEFAULT_STRING);
                            var bitrate = stream.StreamInfo.Bandwidth.StringifyOrDefault(x => $"{x / 1000}kbps", DEFAULT_STRING);
                            streamTable.AddRow(name, resolution, fps, codecs, fileSize, bitrate);
                        }
                        else
                        {
                            streamTable.AddRow(name, resolution, fps, codecs);
                        }
                    }

                    AnsiConsole.Write(streamTable);

                    var infoTableTitle = new TableTitle("Video Info");
                    var infoTable = new Table()
                        .Title(infoTableTitle)
                        .AddColumn(new TableColumn("Key"))
                        .AddColumn(new TableColumn("Value"))
                        .AddRow(new Markup("Streamer"), new Markup(infoVideo.owner.displayName, Style.Plain.Link($"https://twitch.tv/{infoVideo.owner.login}")))
                        .AddRow("Title", infoVideo.title)
                        .AddRow("Length", TimeSpanHFormat.ReusableInstance.Format(@"H\:mm\:ss", TimeSpan.FromSeconds(infoVideo.lengthSeconds)))
                        .AddRow("Category", infoVideo.game?.displayName ?? DEFAULT_STRING)
                        .AddRow("Views", infoVideo.viewCount.ToString("N0", CultureInfo.CurrentCulture))
                        .AddRow("Created at", $"{infoVideo.createdAt.ToUniversalTime():yyyy-MM-dd hh:mm:ss} UTC")
                        .AddRow("Description", infoVideo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd() ?? DEFAULT_STRING);

                    AnsiConsole.Write(infoTable);
                    break;
                }
                case StreamInfoPrintFormat.M3U8:
                {
                    // Parse as m3u8 to verify that it is a valid playlist
                    var m3u8 = M3U8.Parse(playlistString);
                    Console.Write(m3u8.ToString());
                    break;
                }
                case StreamInfoPrintFormat.Json:
                {
                    throw new NotImplementedException("JSON format is not yet supported");
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<(GqlVideoResponse videoInfo, string playlistString)> GetPlaylistInfo(long videoId, string oauth, bool canThrow, ITaskProgress progress)
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

                return (videoInfo, null);
            }

            var playlistString = await TwitchHelper.GetVideoPlaylist(videoId, accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (canThrow && (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements")))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            return (videoInfo, playlistString);
        }

        private static void HandleClip(StreamInfoArgs inputOptions, CliTaskProgress progress)
        {
            var (clipInfo, clipQualities) = GetClipInfo(inputOptions.Id, inputOptions.Format != StreamInfoPrintFormat.Raw, progress).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case StreamInfoPrintFormat.Raw:
                {
                    var stdOut = Console.OpenStandardOutput();
                    JsonSerializer.Serialize(stdOut, clipInfo);
                    Console.WriteLine();
                    JsonSerializer.Serialize(stdOut, clipQualities);
                    break;
                }
                case StreamInfoPrintFormat.Table:
                {
                    const string DEFAULT_STRING = "-";
                    var infoClip = clipInfo.data.clip;

                    var qualityTableTitle = new TableTitle($"{infoClip.broadcaster?.displayName} - {infoClip.title}", Style.Plain.Link($"https://clips.twitch.tv/{inputOptions.Id}"));
                    var qualityTable = new Table()
                        .Title(qualityTableTitle)
                        .AddColumn(new TableColumn("Name"))
                        .AddColumn(new TableColumn("Height"))
                        .AddColumn(new TableColumn("Fps").RightAligned());

                    foreach (var quality in clipQualities.data.clip.videoQualities)
                    {
                        var name = string.Create(CultureInfo.CurrentCulture, $"{quality.quality}p{quality.frameRate:F0}");
                        var height = quality.quality;
                        var fps = quality.frameRate.StringifyOrDefault(x => string.Create(CultureInfo.CurrentCulture, $"{x:F2}"), DEFAULT_STRING);
                        qualityTable.AddRow(name, height, fps);
                    }

                    AnsiConsole.Write(qualityTable);

                    var infoTableTitle = new TableTitle("Clip Info");
                    var infoTable = new Table()
                        .Title(infoTableTitle)
                        .AddColumn(new TableColumn("Key"))
                        .AddColumn(new TableColumn("Value"))
                        .AddRow(new Markup("Streamer"), new Markup(infoClip.broadcaster?.displayName ?? DEFAULT_STRING, Style.Plain.Link($"https://twitch.tv/{infoClip.broadcaster?.login}")))
                        .AddRow("Title", infoClip.title)
                        .AddRow("Length", TimeSpan.FromSeconds(infoClip.durationSeconds).ToString(@"mm\:ss"))
                        .AddRow(new Markup("Clipped by"), new Markup(infoClip.curator?.displayName ?? DEFAULT_STRING, Style.Plain.Link($"https://twitch.tv/{infoClip.curator?.login}")))
                        .AddRow("Category", infoClip.game?.displayName ?? DEFAULT_STRING)
                        .AddRow("Views", infoClip.viewCount.ToString("N0", CultureInfo.CurrentCulture))
                        .AddRow("Created at", $"{infoClip.createdAt.ToUniversalTime():yyyy-MM-dd hh:mm:ss} UTC");

                    if (infoClip.video != null)
                    {
                        var videoOffset = infoClip.videoOffsetSeconds.StringifyOrDefault(x => TimeSpanHFormat.ReusableInstance.Format(@"H\:mm\:ss", TimeSpan.FromSeconds(x)), DEFAULT_STRING);
                        infoTable
                            .AddRow("VOD ID", infoClip.video.id)
                            .AddRow("VOD offset", videoOffset);
                    }

                    AnsiConsole.Write(infoTable);
                    break;
                }
                case StreamInfoPrintFormat.M3U8:
                {
                    var clip = clipQualities.data.clip;

                    var metadata = new M3U8.Metadata
                    {
                        Version = default,
                        MediaSequence = 0,
                        StreamTargetDuration = (uint)clipInfo.data.clip.durationSeconds,
                        TwitchElapsedSeconds = 0,
                        TwitchLiveSequence = default,
                        TwitchTotalSeconds = (uint)clipInfo.data.clip.durationSeconds,
                        Type = M3U8.Metadata.PlaylistType.Event,
                    };

                    var streams = clip.videoQualities.Select(x => new M3U8.Stream(
                        new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, x.quality, x.quality, true, true),
                        new M3U8.Stream.ExtStreamInfo(default, default, default, default, x.quality, x.frameRate),
                        $"{x.sourceURL}?sig={clip.playbackAccessToken.signature}&token={HttpUtility.UrlEncode(clip.playbackAccessToken.value)}"
                    )).ToArray();

                    var m3u8 = new M3U8(metadata, streams);
                    Console.Write(m3u8.ToString());
                    break;
                }
                case StreamInfoPrintFormat.Json:
                {
                    throw new NotImplementedException("JSON format is not yet supported");
                }
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

        private static string StringifyOrDefault<T>(this T value, Func<T, string> stringify, string defaultString) where T : IEquatable<T>
        {
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
    }
}