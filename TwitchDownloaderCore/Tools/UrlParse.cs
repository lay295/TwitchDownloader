using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static class UrlParse
    {
        // TODO: Use source generators when .NET7
        private static readonly Regex TwitchVideoId = new(@"(?<=^|twitch\.tv\/videos\/)\d+(?=$|\?|\s)", RegexOptions.Compiled);
        private static readonly Regex TwitchHighlightId = new(@"(?<=^|twitch\.tv\/\w+\/video\/)\d+(?=$|\?|\s)", RegexOptions.Compiled);
        private static readonly Regex TwitchClipId = new(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:\w+\/clip\/)?)[\w-]+?(?=$|\?|\s)", RegexOptions.Compiled);

        private static readonly Regex KickVideoId = new(@"(?<=kick\.com\/video\/)[\w-]+", RegexOptions.Compiled);
        private static readonly Regex KickClipId = new(@"(?<=kick\.com\/\S+\?clip=)[\w-]+", RegexOptions.Compiled);

        public static readonly Regex UrlTimeCode = new(@"(?<=(?:\?|&)t=)\d+h\d+m\d+s(?=$|\?|\s)", RegexOptions.Compiled);

        public static bool TryParseClip(string url, out VideoPlatform videoPlatform, out string videoId)
        {
            var twitchClipIdMatch = TwitchClipId.Match(url);
            if (twitchClipIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Twitch;
                videoId = twitchClipIdMatch.Value;
                return true;
            }

            var kickClipIdMatch = KickClipId.Match(url);
            if (kickClipIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Kick;
                videoId = kickClipIdMatch.Value;
                return true;
            }

            videoPlatform = VideoPlatform.Twitch;
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
                videoId = twitchVodIdMatch.Value;
                return true;
            }

            var kickVodIdMatch = KickVideoId.Match(url);
            if (kickVodIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Kick;
                videoId = kickVodIdMatch.Value;
                return true;
            }

            videoPlatform = VideoPlatform.Twitch;
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

            videoPlatform = VideoPlatform.Twitch;
            videoType = VideoType.Video;
            videoId = "Don't ignore return value";
            return false;
        }
    }
}
