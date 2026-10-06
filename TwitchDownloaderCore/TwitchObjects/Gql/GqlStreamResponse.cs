namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class StreamBroadcaster
    {
        public string displayName { get; set; }
        public string login { get; set; }
    }

    public class StreamInfo
    {
        public string id { get; set; }
        public string title { get; set; }
        public DateTime createdAt { get; set; }
        public StreamBroadcaster broadcaster { get; set; }
        public Game game { get; set; }
    }

    public class StreamUser
    {
        public StreamInfo stream { get; set; }
    }

    public class StreamData
    {
        public StreamUser user { get; set; }
    }

    public class GqlStreamResponse
    {
        public StreamData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
