using TwitchDownloaderCore.Models.Interfaces;

namespace TwitchDownloaderAvalonia.Models
{
    public partial class QualityOption : ObservableObject
    {
        public QualityOption(IVideoQuality<M3U8.Stream> quality)
        {
            Quality = quality;
            DisplayName = quality.Name;
        }

        public IVideoQuality<M3U8.Stream> Quality { get; }

        [ObservableProperty]
        public partial string DisplayName { get; set; }
    }
}
