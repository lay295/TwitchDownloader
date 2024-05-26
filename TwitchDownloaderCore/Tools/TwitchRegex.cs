using System.Linq;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static partial class TwitchRegex
    {
        [GeneratedRegex("@\"(?<=^|twitch\\.tv\\/videos\\/)\\d+(?=$|\\?|\\s)\"")]
        private static partial Regex VideoId();

        [GeneratedRegex(@"(?<=^|twitch\.tv\/\w+\/v(?:ideo)?\/)\d+(?=$|\?|\s)")]
        private static partial Regex HighlightId();

        [GeneratedRegex(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:\w+\/clip\/)?)[\w-]+?(?=$|\?|\s)")]
        private static partial Regex ClipId();

        [GeneratedRegex(@"(?<=(?:\?|&)t=)\d+h\d+m\d+s(?=$|\?|\s)")]
        public static partial Regex UrlTimeCode();

        [GeneratedRegex(@"(?<=(?:\s|^)(?:4Head|Anon|Bi(?:bleThumb|tBoss)|bday|C(?:h(?:eer|arity)|orgo)|cheerwal|D(?:ansGame|oodleCheer)|EleGiggle|F(?:rankerZ|ailFish)|Goal|H(?:eyGuys|olidayCheer)|K(?:appa|reygasm)|M(?:rDestructoid|uxy)|NotLikeThis|P(?:arty|ride|JSalt)|RIPCheer|S(?:coops|h(?:owLove|amrock)|eemsGood|wiftRage|treamlabs)|TriHard|uni|VoHiYo))[1-9]\d{0,6}(?=\s|$)")]
        public static partial Regex BitsRegex();

        /// <returns>A <see cref="Match"/> of the video's id or <see langword="null"/>.</returns>
        public static Match MatchVideoId(string text)
        {
            var videoIdMatch = VideoId().Match(text);
            if (videoIdMatch.Success)
            {
                return videoIdMatch;
            }

            var highlightIdMatch = HighlightId().Match(text);
            if (highlightIdMatch.Success)
            {
                return highlightIdMatch;
            }

            return null;
        }

        /// <returns>A <see cref="Match"/> of the clip's id or <see langword="null"/>.</returns>
        public static Match MatchClipId(string text)
        {
            var clipIdMatch = ClipId().Match(text);
            if (clipIdMatch.Success && !clipIdMatch.Value.All(char.IsDigit))
            {
                return clipIdMatch;
            }

            return null;
        }

        /// <returns>A <see cref="Match"/> of the video/clip's id or <see langword="null"/>.</returns>
        public static Match MatchVideoOrClipId(string text)
        {
            var videoIdMatch = MatchVideoId(text);
            if (videoIdMatch is { Success: true })
            {
                return videoIdMatch;
            }

            var clipIdMatch = MatchClipId(text);
            if (clipIdMatch is { Success: true })
            {
                return clipIdMatch;
            }

            return null;
        }
    }
}