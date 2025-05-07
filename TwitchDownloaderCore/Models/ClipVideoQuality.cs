using TwitchDownloaderCore.Models.Interfaces;
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ShareClipRenderStatusVideoQuality;

namespace TwitchDownloaderCore.Models
{
    public class ClipVideoQuality : IVideoQuality<ClipQuality>
    {
        public ClipQuality Item { get; }

        public string Name { get; }

        public Resolution Resolution { get; }

        public bool IsSource { get; }

        public ClipVideoQuality(ClipQuality item, string name, bool isSource)
        {
            Item = item;
            Name = name;
            IsSource = isSource;

            if (uint.TryParse(item.quality, out var frameHeight))
            {
                Resolution = new Resolution(frameHeight);
            }
        }
    }
}