using TwitchDownloaderCore.Models.Interfaces;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderAvalonia.Models
{
    public partial class ClipQualityOption : ObservableObject
    {
        public ClipQualityOption(IVideoQuality<ShareClipRenderStatusVideoQuality> quality)
        {
            Quality = quality;
            DisplayName = quality.Name;
        }

        public IVideoQuality<ShareClipRenderStatusVideoQuality> Quality { get; }

        [ObservableProperty]
        public partial string DisplayName { get; set; }
    }
}
