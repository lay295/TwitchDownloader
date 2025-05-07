using System;
using System.Linq;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Extensions
{
    public static class M3U8Extensions
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

        /// <returns>
        /// A <see cref="string"/> representing the <paramref name="stream"/>'s <see cref="M3U8.Stream.ExtStreamInfo.Resolution"/>
        /// and <see cref="M3U8.Stream.ExtStreamInfo.Framerate"/> in the format of "{resolution}p{framerate}" or <see langword="null"/>
        /// </returns>
        public static string GetResolutionFramerateString(this M3U8.Stream stream, bool appendSource = true)
        {
            const string RESOLUTION_FRAMERATE_PATTERN = /*lang=regex*/@"\d{3,4}p\d{2,3}";

            var mediaInfo = stream.MediaInfo;
            if (stream.IsAudioOnly() || Regex.IsMatch(mediaInfo.Name, RESOLUTION_FRAMERATE_PATTERN))
            {
                return mediaInfo.Name;
            }

            var streamInfo = stream.StreamInfo;
            if (Regex.IsMatch(streamInfo.Video, RESOLUTION_FRAMERATE_PATTERN))
            {
                var hyphenIndex = streamInfo.Video.IndexOf('-');
                return hyphenIndex > 0 ? streamInfo.Video[..hyphenIndex] : streamInfo.Video;
            }

            if (Regex.IsMatch(mediaInfo.GroupId, RESOLUTION_FRAMERATE_PATTERN))
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
    }
}