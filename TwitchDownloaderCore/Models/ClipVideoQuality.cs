using TwitchDownloaderCore.Models.Interfaces;
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ShareClipRenderStatusVideoQuality;

namespace TwitchDownloaderCore.Models
{
    public class ClipVideoQuality : IVideoQuality<ClipQuality>
    {
        public ClipQuality Item { get; }

        public string Name { get; }

        public Resolution Resolution { get; }

        public decimal Framerate { get; }

        public bool IsSource { get; }

        public ClipVideoQuality(ClipQuality item, string name, Resolution resolution, bool isSource)
        {
            Item = item;
            Name = name;
            Resolution = resolution;
            Framerate = item.frameRate;
            IsSource = isSource;
        }
    }
}