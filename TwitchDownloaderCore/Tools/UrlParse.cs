using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools
{
    public enum VideoPlatform
    {
        Twitch,
        Kick,
        Youtube
    }

    public class UrlParse
    {
        public static bool TryParseClip(string url, out VideoPlatform videoPlatform, out string videoId)
        {
            var twitchClipIdRegex = new Regex(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:\S+\/clip)?\/?)[\w-]+?(?=$|\?)");
            var twitchClipIdMatch = twitchClipIdRegex.Match(url);

            if (twitchClipIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Twitch;
                videoId = twitchClipIdMatch.Value;
                return true;
            }

            var kickClipIdRegex = new Regex(@"(?<=kick\.com\/\S+\?clip=)[\w-]+");
            var kickClipIdMatch = kickClipIdRegex.Match(url);

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
            var twitchVodRegex = new Regex(@"(?<=^|twitch\.tv\/videos\/)\d+(?=$|\?)");
            var twitchVodIdMatch = twitchVodRegex.Match(url);

            if (twitchVodIdMatch.Success)
            {
                videoPlatform = VideoPlatform.Twitch;
                videoId = twitchVodIdMatch.Value;
                return true;
            }

            var kickVodIdRegex = new Regex(@"(?<=kick\.com\/video\/)[\w-]+");
            var kickVodIdMatch = kickVodIdRegex.Match(url);

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
    }
}
