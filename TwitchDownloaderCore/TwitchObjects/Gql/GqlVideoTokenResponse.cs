namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class GqlVideoTokenData
    {
        public PlaybackAccessToken videoPlaybackAccessToken { get; set; }
    }

    public class GqlVideoTokenResponse
    {
        public GqlVideoTokenData data { get; set; }
        public Extensions extensions { get; set; }
    }

    public class PlaybackAccessToken
    {
        public string value { get; set; }
        public string signature { get; set; }
    }
}
