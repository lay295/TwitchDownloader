using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Kick;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;

namespace TwitchDownloaderCore
{
    public class PlatformHelper
    {
        public static async Task<IVideoInfo> GetClipInfo(VideoPlatform videoPlatform, string clipId)
        {
            if (videoPlatform == VideoPlatform.Twitch)
            {
                IVideoInfo clipInfo = await TwitchHelper.GetClipInfo(clipId);
                List<GqlClipTokenResponse> clipResponse = await TwitchHelper.GetClipLinks(clipId);
                clipInfo.VideoQualities = new List<VideoQuality>();
                foreach (var clip in clipResponse[0].data.clip.videoQualities)
                {
                    clipInfo.VideoQualities.Add(new VideoQuality { Quality = clip.quality, Framerate = clip.frameRate, SourceUrl = clip.sourceURL });
                }
                return clipInfo;
            }

            if (videoPlatform == VideoPlatform.Kick)
            {
                return await KickHelper.GetClipInfo(clipId);
            }

            throw new NotImplementedException();
        }

        public static async Task<IVideoInfo> GetVideoInfo(VideoPlatform videoPlatform, string videoId, string Oauth = "")
        {
            if (videoPlatform == VideoPlatform.Twitch)
            {
                return await TwitchHelper.GetVideoInfo(int.Parse(videoId));
            }

            if (videoPlatform == VideoPlatform.Kick)
            {
                return await KickHelper.GetVideoInfo(videoId);
            }

            throw new NotImplementedException();
        }
    }
}
