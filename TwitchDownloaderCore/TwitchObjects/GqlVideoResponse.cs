using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class VideoOwner
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class Video
    {
        public string title { get; set; }
        public List<string> thumbnailURLs { get; set; }
        public DateTime createdAt { get; set; }
        public int lengthSeconds { get; set; }
        public VideoOwner owner { get; set; }
    }

    public class VideoData
    {
        public Video video { get; set; }
    }

    public class GqlVideoResponse
    {
        public VideoData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
