using System;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class VideoOwner
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class VideoGame
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class VideoInfo
    {
        public string title { get; set; }
        public List<string> thumbnailURLs { get; set; }
        public DateTime createdAt { get; set; }
        public int lengthSeconds { get; set; }
        public VideoOwner owner { get; set; }
        public int viewCount { get; set; }
        public VideoGame game { get; set; }
        public string description { get; set; }
    }

    public class VideoData
    {
        public VideoInfo video { get; set; }
    }

    public class GqlVideoResponse
    {
        public VideoData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
