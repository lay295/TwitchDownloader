using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static class TwitchRegex
    {
        // TODO: Use source generators when .NET7
        private static readonly Regex VideoId = new(@"(?<=^|twitch\.tv\/videos\/)\d+(?=\/?(?:$|\?))", RegexOptions.Compiled);
        private static readonly Regex HighlightId = new(@"(?<=^|twitch\.tv\/\w+\/v(?:ideo)?\/)\d+(?=\/?(?:$|\?))", RegexOptions.Compiled);
        private static readonly Regex ClipId = new(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:\w+\/clip\/)?)[\w-]+?(?=\/?(?:$|\?))", RegexOptions.Compiled);

        public static readonly Regex UrlTimeCode = new(@"(?<=(?:\?|&)t=)\d+h\d+m\d+s(?=$|\?|\s)", RegexOptions.Compiled);
        public static readonly Regex BitsRegex = new(
            @"(?<=(?:\s|^)(?:4Head|Anon|Bi(?:bleThumb|tBoss)|bday|C(?:h(?:eer|arity)|orgo)|cheerwal|D(?:ansGame|oodleCheer)|EleGiggle|F(?:rankerZ|ailFish)|Goal|H(?:eyGuys|olidayCheer)|K(?:appa|reygasm)|M(?:rDestructoid|uxy)|NotLikeThis|P(?:arty|ride|JSalt)|RIPCheer|S(?:coops|h(?:owLove|amrock)|eemsGood|wiftRage|treamlabs)|TriHard|uni|VoHiYo))[1-9]\d{0,6}(?=\s|$)",
            RegexOptions.Compiled);

        /// <returns>A <see cref="Match"/> of the video's id or <see langword="null"/>.</returns>
        [return: MaybeNull]
        public static Match MatchVideoId(string text)
        {
            text = text.Trim();

            var videoIdMatch = VideoId.Match(text);
            if (videoIdMatch.Success)
            {
                return videoIdMatch;
            }

            var highlightIdMatch = HighlightId.Match(text);
            if (highlightIdMatch.Success)
            {
                return highlightIdMatch;
            }

            return null;
        }

        /// <returns>A <see cref="Match"/> of the clip's id or <see langword="null"/>.</returns>
        [return: MaybeNull]
        public static Match MatchClipId(string text)
        {
            text = text.Trim();

            var clipIdMatch = ClipId.Match(text);
            if (clipIdMatch.Success && !clipIdMatch.Value.All(char.IsDigit))
            {
                return clipIdMatch;
            }

            return null;
        }

        /// <returns>A <see cref="Match"/> of the video/clip's id or <see langword="null"/>.</returns>
        [return: MaybeNull]
        public static Match MatchVideoOrClipId(string text)
        {
            text = text.Trim();

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