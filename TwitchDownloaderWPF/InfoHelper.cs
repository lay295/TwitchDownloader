using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwitchDownloader
{
    class InfoHelper
    {
        public static async Task<BitmapImage> GetThumb(string thumbUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                BitmapImage img = new BitmapImage();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.BeginInit();
                img.StreamSource = await client.GetStreamAsync(thumbUrl);
                img.EndInit();
                return img;
            }
        }
    }
}
