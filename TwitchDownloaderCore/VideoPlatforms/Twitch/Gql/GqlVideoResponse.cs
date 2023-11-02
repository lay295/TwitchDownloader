using System;
using System.Collections.Generic;
using System.Linq;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCore.VideoPlatforms.Twitch.Gql
{
    public class VideoOwner
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
        public Game game { get; set; }
        /// <remarks>
        /// Some values, such as newlines, are repeated twice for some reason.
        /// This can be filtered out with: <code>description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd()</code>
        /// </remarks>
        public string description { get; set; }
    }

    public class VideoData
    {
        public VideoInfo video { get; set; }
    }

    public class GqlVideoResponse : IVideoInfo
    {
        public VideoData data { get; set; }
        public Extensions extensions { get; set; }

        public string ThumbnailUrl => data?.video?.thumbnailURLs?.FirstOrDefault();

        public DateTime CreatedAt => data?.video?.createdAt ?? DateTime.MinValue;

        public string StreamerName => data?.video?.owner?.displayName;

        public string Title => data?.video?.title;

        public int Duration => data?.video?.lengthSeconds ?? 0;

        public int ViewCount => data?.video?.viewCount ?? 0;

        public string Game => data?.video?.game?.displayName;

        public string VideoUrl => null;
        public List<VideoQuality> VideoQualities { get; set; }
    }
}
