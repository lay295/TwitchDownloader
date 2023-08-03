using System;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class ClipBroadcaster
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class ClipVideo
    {
        public string id { get; set; }
    }

    public class ClipGame
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class Clip
    {
        public string title { get; set; }
        public string thumbnailURL { get; set; }
        public DateTime createdAt { get; set; }
        public int durationSeconds { get; set; }
        public ClipBroadcaster broadcaster { get; set; }
        public int? videoOffsetSeconds { get; set; }
        public ClipVideo video { get; set; }
        public int viewCount { get; set; }
        public ClipGame game { get; set; }
    }

    public class ClipData
    {
        public Clip clip { get; set; }
    }

    public class GqlClipResponse
    {
        public ClipData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
