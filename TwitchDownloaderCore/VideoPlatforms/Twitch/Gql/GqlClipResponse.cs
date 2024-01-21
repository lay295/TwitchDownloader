using System;
using System.Collections.Generic;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCore.VideoPlatforms.Twitch.Gql
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
        public Game game { get; set; }
    }

    public class ClipData
    {
        public Clip clip { get; set; }
    }

    public class GqlClipResponse : IVideoInfo
    {
        public ClipData data { get; set; }
        public Extensions extensions { get; set; }
        public string Id { get; set; }
        public string ThumbnailUrl => data?.clip?.thumbnailURL;
        public DateTime CreatedAt => data?.clip?.createdAt ?? DateTime.MinValue;
        public string StreamerName => data?.clip?.broadcaster?.displayName;
        public string Title => data?.clip?.title;
        public int Duration => data?.clip?.durationSeconds ?? 0;
        public int ViewCount => data?.clip?.viewCount ?? 0;
        public string Game => data?.clip?.game?.displayName;
    }
}
