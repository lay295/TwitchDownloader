using System;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class GqlStreamMetadataResponse
    {
        public GqlStreamMetadataData data { get; set; }
    }

    public class GqlStreamMetadataData
    {
        public StreamMetadataUser user { get; set; }
    }

    public class StreamMetadataUser
    {
        public string id { get; set; }
        public string login { get; set; }
        public string displayName { get; set; }
        public StreamMetadataStream stream { get; set; }
    }

    public class StreamMetadataStream
    {
        public string id { get; set; }
        public DateTimeOffset createdAt { get; set; }
    }
}
