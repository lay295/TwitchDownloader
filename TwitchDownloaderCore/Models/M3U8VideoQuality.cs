using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models.Interfaces;

namespace TwitchDownloaderCore.Models
{
    public sealed record M3U8VideoQuality : IVideoQuality<M3U8.Stream>
    {
        public M3U8.Stream Item { get; }

        public string Name { get; }

        public Resolution Resolution { get; }

        public decimal Framerate { get; }

        public bool IsSource { get; }

        public string Path { get; }

        public int BitRate { get; }

        internal M3U8VideoQuality(M3U8.Stream item, string name)
        {
            Item = item;
            Name = name;
            Resolution = item.StreamInfo.Resolution;
            Framerate = item.StreamInfo.Framerate;
            IsSource = item.IsSource();
            Path = item.Path;
            BitRate = item.StreamInfo.Bandwidth;
        }

        public override string ToString() => Name;
    }
}