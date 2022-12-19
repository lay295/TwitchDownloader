using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class ClipToken
    {
        public string id { get; set; }
        public PlaybackAccessToken playbackAccessToken { get; set; }
        public List<VideoQuality> videoQualities { get; set; }
        public string __typename { get; set; }
    }

    public class ClipTokenData
    {
        public ClipToken clip { get; set; }
    }

    public class PlaybackAccessToken
    {
        public string signature { get; set; }
        public string value { get; set; }
        public string __typename { get; set; }
    }

    public class GqlClipTokenResponse
    {
        public ClipTokenData data { get; set; }
        public Extensions extensions { get; set; }
    }

    public class VideoQuality
    {
        public int frameRate { get; set; }
        public string quality { get; set; }
        public string sourceURL { get; set; }
        public string __typename { get; set; }
    }
}
