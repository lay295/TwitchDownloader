using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static class IdParse
    {
        // TODO: Use source generators when .NET7
        // Note: The oldest VODs use 8-digit IDs (like 47560181). Currently, VODs use 10-digit, but we will support up to 11 for the future proofing.
        // The shortest word I have seen in the clip slugs was 3 characters long, so we can assume slugs must have a minimum of 9 characters
        private static readonly Regex TwitchVideoId = new(@"(?<=^|twitch\.tv\/videos\/)\d{8,11}(?=$|\?|\s)", RegexOptions.Compiled);
        private static readonly Regex TwitchHighlightId = new(@"(?<=^|twitch\.tv\/\w+\/video\/)\d{8,11}(?=$|\?|\s)", RegexOptions.Compiled);
        private static readonly Regex TwitchClipId = new(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:\w+\/clip\/)?)[\w-]{9,}(?=$|\?|\s)", RegexOptions.Compiled);

        // Kick clip IDs (excluding clip_) are currently, and seem to always have been, 26 digits long
        private static readonly Regex KickVideoId = new(@"(?<=^|kick\.com\/video\/)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?=$|\?|\s)", RegexOptions.Compiled);
        private static readonly Regex KickClipId = new(@"(?<=^|kick\.com\/\w+\?clip=)clip_[0-9A-Z]{26}(?=$|\?|\s)", RegexOptions.Compiled);

        public static bool TryParseClip(string url, out VideoPlatform videoPlatform, out string videoId)
        {
            // Kick match must come first since it explicitly matches 'clip_' meanwhile Twitch just matches any alphanumeric
            var kickClipIdMatch = KickClipId.Match(url);
            if (kickClipIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Kick;
                videoId = kickClipIdMatch.Value;
                return true;
            }

            var twitchClipIdMatch = TwitchClipId.Match(url);
            if (twitchClipIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Twitch;
                videoId = twitchClipIdMatch.Value;
                return true;
            }

            videoPlatform = VideoPlatform.Unknown;
            videoId = "Don't ignore return value";
            return false;
        }

        public static bool TryParseVod(string url, out VideoPlatform videoPlatform, out string videoId)
        {
            var twitchVodIdMatch = TwitchVideoId.Match(url);
            if (twitchVodIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Twitch;
                videoId = twitchVodIdMatch.Value;
                return true;
            }

            var twitchHighlightIdMatch = TwitchHighlightId.Match(url);
            if (twitchHighlightIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Twitch;
                videoId = twitchHighlightIdMatch.Value;
                return true;
            }

            var kickVodIdMatch = KickVideoId.Match(url);
            if (kickVodIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Kick;
                videoId = kickVodIdMatch.Value;
                return true;
            }

            videoPlatform = VideoPlatform.Unknown;
            videoId = "Don't ignore return value";
            return false;
        }

        public static bool TryParseVideoOrClipId(string url, out VideoPlatform videoPlatform, out VideoType videoType, out string videoId)
        {
            if (TryParseVod(url, out videoPlatform, out videoId))
            {
                videoType = VideoType.Video;
                return true;
            }

            if (TryParseClip(url, out videoPlatform, out videoId))
            {
                videoType = VideoType.Clip;
                return true;
            }

            videoPlatform = VideoPlatform.Unknown;
            videoType = VideoType.Unknown;
            videoId = "Don't ignore return value";
            return false;
        }
    }
}
