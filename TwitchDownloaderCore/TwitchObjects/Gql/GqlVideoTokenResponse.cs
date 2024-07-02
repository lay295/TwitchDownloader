﻿namespace TwitchDownloaderCore.TwitchObjects.Gql
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
    }
}
