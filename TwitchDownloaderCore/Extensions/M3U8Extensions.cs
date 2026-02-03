using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Api;

namespace TwitchDownloaderCore.Extensions
{
    public static partial class M3U8Extensions
    {
        public static void SortStreamsByQuality(this M3U8 m3u8)
        {
            var streams = m3u8.Streams;
            if (streams.Length == 0)
            {
                return;
            }

            if (m3u8.Streams.Any(x => x.IsPlaylist))
            {
                Array.Sort(m3u8.Streams, new M3U8StreamQualityComparer());
            }
        }

        [GeneratedRegex("""\d{3,4}p\d{2,3}""")]
        private static partial Regex ResolutionFramerateRegex { get; }

        /// <returns>
        /// A <see cref="string"/> representing the <paramref name="stream"/>'s <see cref="M3U8.Stream.ExtStreamInfo.Resolution"/>
        /// and <see cref="M3U8.Stream.ExtStreamInfo.Framerate"/> in the format of "{resolution}p{framerate}" or <see langword="null"/>
        /// </returns>
        public static string GetResolutionFramerateString(this M3U8.Stream stream, bool appendSource = true)
        {
            var mediaInfo = stream.MediaInfo;
            if (stream.IsAudioOnly() || ResolutionFramerateRegex.IsMatch(mediaInfo.Name))
            {
                return mediaInfo.Name;
            }

            var streamInfo = stream.StreamInfo;
            if (ResolutionFramerateRegex.IsMatch(streamInfo.Video))
            {
                var hyphenIndex = streamInfo.Video.IndexOf('-');
                return hyphenIndex > 0 ? streamInfo.Video[..hyphenIndex] : streamInfo.Video;
            }

            if (ResolutionFramerateRegex.IsMatch(mediaInfo.GroupId))
            {
                var hyphenIndex = mediaInfo.GroupId.IndexOf('-');
                return hyphenIndex > 0 ? mediaInfo.GroupId[..hyphenIndex] : mediaInfo.GroupId;
            }

            if (streamInfo.Resolution == default)
            {
                return stream.IsSource()
                    ? "Source"
                    : "";
            }

            var frameHeight = streamInfo.Resolution.Height;

            if (streamInfo.Framerate == 0)
            {
                return appendSource && stream.IsSource()
                    ? $"{frameHeight}p (Source)"
                    : $"{frameHeight}p";
            }

            // Some M3U8 responses have framerate values up to 2fps more/less than the typical framerate.
            var frameRate = (uint)(Math.Round(streamInfo.Framerate / 10) * 10);

            return appendSource && stream.IsSource()
                ? $"{frameHeight}p{frameRate} (Source)"
                : $"{frameHeight}p{frameRate}";
        }

        public static bool IsSource(this M3U8.Stream stream)
            => stream.MediaInfo.Name.Contains("source", StringComparison.OrdinalIgnoreCase) ||
               stream.MediaInfo.GroupId.Equals("chunked", StringComparison.OrdinalIgnoreCase);

        public static bool IsAudioOnly(this M3U8.Stream stream)
            => stream.MediaInfo.Name.Contains("audio", StringComparison.OrdinalIgnoreCase);

        public static M3U8 WithUnavailableMedia(this M3U8 m3u8)
        {
            if (m3u8.Streams.Length is 0)
            {
                return m3u8;
            }

            const string UNAVAILABLE_MEDIA_KEY = "com.amazon.ivs.unavailable-media";
            if (!m3u8.FileMetadata.SessionData.TryGetValue(UNAVAILABLE_MEDIA_KEY, out var unavailableMediaBase64))
            {
                return m3u8;
            }

            var unavailableMedia = JsonSerializer.Deserialize<UnavailableMedia[]>(Convert.FromBase64String(unavailableMediaBase64));
            if (unavailableMedia is not { Length: > 0 })
            {
                return m3u8;
            }

            // There's probably a better way to do this, but it doesn't really matter
            var pathParts = m3u8.Streams.First().Path.Split('/');
            pathParts[^2] = "{0}"; // *.cloudfront.net/abc_123/quality/index-dvr.m3u8
            var pathFormat = string.Join('/', pathParts);

            var unavailableStreams = unavailableMedia.Select(x =>
            {
                var mediaInfo = new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, x.GroupId, x.Name, true, true);
                var streamInfo = new M3U8.Stream.ExtStreamInfo(0, x.Bandwidth, x.Codecs.Split(','), M3U8.Stream.ExtStreamInfo.StreamResolution.Parse(x.Resolution), x.GroupId, x.FrameRate);
                var path = string.Format(pathFormat, x.GroupId);
                return new M3U8.Stream(mediaInfo, streamInfo, path);
            });

            var allStreams = unavailableStreams
                .Where(x => m3u8.Streams.All(y => x.Path != y.Path))
                .Concat(m3u8.Streams);

            var newM3u8 = m3u8 with { Streams = allStreams.ToArray() };
            newM3u8.SortStreamsByQuality();

            return newM3u8;
        }
    }
}