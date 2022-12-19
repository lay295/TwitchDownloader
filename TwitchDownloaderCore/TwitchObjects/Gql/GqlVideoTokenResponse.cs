using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class GqlVideoData
    {
        public VideoPlaybackAccessToken videoPlaybackAccessToken { get; set; }
    }

    public class GqlVideoTokenResponse
    {
        public GqlVideoData data { get; set; }
        public Extensions extensions { get; set; }
    }

    public class VideoPlaybackAccessToken
    {
        public string value { get; set; }
        public string signature { get; set; }
        public string __typename { get; set; }
    }
}
