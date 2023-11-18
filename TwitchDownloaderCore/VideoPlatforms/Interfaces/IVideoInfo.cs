using System;

namespace TwitchDownloaderCore.VideoPlatforms.Interfaces
{
    public interface IVideoInfo
    {
        public string Id { get; }
        public string ThumbnailUrl { get; }
        public DateTime CreatedAt { get; }
        public string StreamerName { get; }
        public string Title { get; }
        public int Duration { get; }
        public int ViewCount { get; }
        public string Game { get; }
    }
}