using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwitchDownloader
{
    class InfoHelper
    {
        public const string THUMBNAIL_MISSING_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";
        private static readonly HttpClient _httpclient = new();

        public static async Task<BitmapImage> GetThumb(string thumbUrl)
        {
            BitmapImage img = new();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.BeginInit();
            img.StreamSource = await _httpclient.GetStreamAsync(thumbUrl);
            img.EndInit();
            return img;
        }

        public static async Task<(bool success, BitmapImage image)> TryGetThumb(string thumbUrl)
        {
            try
            {
                return (true, await GetThumb(thumbUrl));
            }
            catch
            {
                return (false, null);
            }
        }
    }
}
