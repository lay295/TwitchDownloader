using TwitchDownloaderCore.Models.Interfaces;
using ClipQuality = TwitchDownloaderCore.TwitchObjects.Gql.ClipVideoQuality;

namespace TwitchDownloaderCore.Models
{
    public class ClipVideoQuality : IVideoQuality<ClipQuality>
    {
        public ClipQuality Item { get; }

        public string Name { get; }

        public bool IsSource { get; }

        public ClipVideoQuality(ClipQuality item, string name, bool isSource)
        {
            Item = item;
            Name = name;
            IsSource = isSource;
        }
    }
}