using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static partial class IdParse
    {
        [GeneratedRegex("""(?<=^|twitch\.tv\/videos\/)\d+(?=\/?(?:$|\?))""")]
        private static partial Regex VideoId { get; }

        [GeneratedRegex("""(?<=^|twitch\.tv\/\w+\/v(?:ideo)?\/)\d+(?=\/?(?:$|\?))""")]
        private static partial Regex HighlightId { get; }

        [GeneratedRegex("""(?<=^|(?:clips\.)?twitch\.tv\/(?:\w+\/clip\/)?)[\w-]+?(?=\/?(?:$|\?))""")]
        private static partial Regex ClipId { get; }

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