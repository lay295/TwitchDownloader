using TwitchDownloaderCore.Models.Interfaces;
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ShareClipRenderStatusVideoQuality;

namespace TwitchDownloaderCore.Models
{
    public sealed record ClipVideoQuality : IVideoQuality<ClipQuality>
    {
        public ClipQuality Item { get; }

        public string Name { get; }

        public Resolution Resolution { get; }

        public decimal Framerate { get; }

        public bool IsSource { get; }

        public string Path { get; }

        public ClipVideoQuality(ClipQuality item, string name, Resolution resolution, bool isSource)
        {
            Item = item;
            Name = name;
            Resolution = resolution;
            Framerate = item.frameRate;
            IsSource = isSource;
            Path = item.sourceURL;
        }

        public override string ToString() => Name;
    }
}