using System;
using System.Linq;
using System.Text.RegularExpressions;
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

        private static readonly Regex UserQualityStringRegex = new(@"(?:^|\s)(?:(?<Width>\d{3,4})x)?(?<Height>\d{3,4})p?(?<Framerate>\d{1,3})?(?:$|\s)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static M3U8.Stream GetStreamOfQuality(this M3U8 m3u8, string qualityString)
        {
            var streams = m3u8.Streams;
            if (streams.Length == 0)
            {
                throw new ArgumentException(nameof(m3u8), "M3U8 does not contain any streams.");
            }

            if (qualityString is null)
            {
                return m3u8.BestQualityStream();
            }

            if (qualityString.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
                streams.FirstOrDefault(x => x.MediaInfo.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)) is { } audioStream)
            {
                return audioStream;
            }

            if (!qualityString.Contains('x') && qualityString.Contains('p'))
            {
                foreach (var stream in streams)
                {
                    if (qualityString.Equals(stream.StreamInfo.Video, StringComparison.OrdinalIgnoreCase) || qualityString.Equals(stream.MediaInfo.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return stream;
                    }
                }
            }

            var qualityStringMatch = UserQualityStringRegex.Match(qualityString);
            if (!qualityStringMatch.Success)
            {
                return m3u8.BestQualityStream();
            }

            var desiredWidth = qualityStringMatch.Groups["Width"];
            var desiredHeight = qualityStringMatch.Groups["Height"];
            var desiredFramerate = qualityStringMatch.Groups["Framerate"];

            var filteredStreams = streams
                .WhereOnlyIf(x => x.StreamInfo.Resolution.Width == int.Parse(desiredWidth.ValueSpan), desiredWidth.Success)
                .WhereOnlyIf(x => x.StreamInfo.Resolution.Height == int.Parse(desiredHeight.ValueSpan), desiredHeight.Success)
                .WhereOnlyIf(x => Math.Abs(x.StreamInfo.Framerate - int.Parse(desiredFramerate.ValueSpan)) <= 2, desiredFramerate.Success)
                .ToArray();

            return filteredStreams.Length switch
            {
                1 => filteredStreams[0],
                2 when !desiredFramerate.Success => filteredStreams.First(x => Math.Abs(x.StreamInfo.Framerate - 30) <= 2),
                _ => m3u8.BestQualityStream()
            };
        }

        /// <returns>
        /// A <see cref="string"/> representing the <paramref name="streamInfo"/>'s <see cref="M3U8.Stream.ExtStreamInfo.Resolution"/>
        /// and <see cref="M3U8.Stream.ExtStreamInfo.Framerate"/> in the format of "{resolution}p{framerate}" or <see langword="null"/>
        /// </returns>
        public static string GetResolutionFramerateString(this M3U8.Stream stream)
        {
            var mediaInfo = stream.MediaInfo;
            if (mediaInfo.Name.Contains("audio", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(mediaInfo.Name, @"\d{3,4}p\d{2,3}"))
            {
                return mediaInfo.Name;
            }

            var streamInfo = stream.StreamInfo;
            if (Regex.IsMatch(streamInfo.Video, @"\d{3,4}p\d{2,3}"))
            {
                return streamInfo.Video;
            }

            if (Regex.IsMatch(mediaInfo.GroupId, @"\d{3,4}p\d{2,3}"))
            {
                return mediaInfo.GroupId;
            }

            if (streamInfo.Resolution == default)
            {
                return "";
            }

            var frameHeight = streamInfo.Resolution.Height;

            if (streamInfo.Framerate == default)
            {
                return $"{frameHeight}p";
            }

            // Some M3U8 responses have framerate values up to 2fps more/less than the typical framerate.
            var frameRate = (uint)(Math.Round(streamInfo.Framerate / 10) * 10);

            return $"{frameHeight}p{frameRate}";
        }

        /// <summary>
        /// Returns the best quality stream from the provided M3U8.
        /// </summary>
        public static M3U8.Stream BestQualityStream(this M3U8 m3u8)
        {
            var source = Array.Find(
                m3u8.Streams, x => x.MediaInfo.Name.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                    x.MediaInfo.GroupId.Equals("chunked", StringComparison.OrdinalIgnoreCase));
            return source ?? m3u8.Streams.MaxBy(x => x.StreamInfo.Resolution.Width * x.StreamInfo.Resolution.Height * x.StreamInfo.Framerate);
        }
    }
}