namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class GqlStreamTokenData
    {
        public PlaybackAccessToken streamPlaybackAccessToken { get; set; }
    }

    public class GqlStreamTokenResponse
    {
        public GqlStreamTokenData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
