using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwitchDownloaderWPF.Services
{
    public static class ThumbnailService
    {
        public const string THUMBNAIL_MISSING_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";
        private static readonly HttpClient HttpClient = new();

        public static async Task<BitmapImage> GetThumb(string thumbUrl)
        {
            if (string.IsNullOrWhiteSpace(thumbUrl))
            {
                return null;
            }

            BitmapImage img = new BitmapImage();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.BeginInit();
            img.StreamSource = await HttpClient.GetStreamAsync(thumbUrl);
            img.EndInit();
            return img;
        }

        public static async Task<(bool success, BitmapImage image)> TryGetThumb(string thumbUrl)
        {
            try
            {
                var thumb = await GetThumb(thumbUrl);
                return (thumb != null, thumb);
            }
            catch
            {
                return (false, null);
            }
        }
    }
}
