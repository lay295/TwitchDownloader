using System;
using System.Windows.Media.Imaging;

namespace TwitchDownloaderWPF.Services
{
    public static class ThumbnailService
    {
        public const string THUMBNAIL_MISSING_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";

        public static BitmapImage GetThumb(string thumbUrl, BitmapCacheOption cacheOption = BitmapCacheOption.OnLoad)
        {
            ArgumentNullException.ThrowIfNull(thumbUrl);

            var img = new BitmapImage { CacheOption = cacheOption };
            img.BeginInit();
            img.UriSource = new Uri(thumbUrl);
            img.EndInit();
            img.DownloadCompleted += static (sender, _) =>
            {
                if (sender is BitmapImage { CanFreeze: true } image)
                {
                    image.Freeze();
                }
            };
            return img;
        }

        public static bool TryGetThumb(string thumbUrl, out BitmapImage thumbnail)
        {
            if (string.IsNullOrWhiteSpace(thumbUrl))
            {
                thumbnail = null;
                return false;
            }

            try
            {
                thumbnail = GetThumb(thumbUrl);
                return thumbnail != null;
            }
            catch
            {
                thumbnail = null;
                return false;
            }
        }
    }
}
