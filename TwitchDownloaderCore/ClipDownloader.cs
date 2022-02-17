using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore
{
    public class ClipDownloader
    {
        ClipDownloadOptions downloadOptions;

        public ClipDownloader(ClipDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
        }

        public async Task DownloadAsync()
        {
            JArray taskLinks = await TwitchHelper.GetClipLinks(downloadOptions.Id);

            string downloadUrl = "";

            foreach (var quality in taskLinks[0]["data"]["clip"]["videoQualities"])
            {
                if (quality["quality"].ToString() + "p" + (quality["frameRate"].ToString() == "30" ? "" : quality["frameRate"].ToString()) == downloadOptions.Quality)
                    downloadUrl = quality["sourceURL"].ToString();
            }

            if (downloadUrl == "")
                downloadUrl = taskLinks[0]["data"]["clip"]["videoQualities"].First["sourceURL"].ToString();

            downloadUrl += "?sig=" + taskLinks[0]["data"]["clip"]["playbackAccessToken"]["signature"] + "&token=" + HttpUtility.UrlEncode(taskLinks[0]["data"]["clip"]["playbackAccessToken"]["value"].ToString());

            using (WebClient client = new WebClient())
                await client.DownloadFileTaskAsync(downloadUrl, downloadOptions.Filename);
        }
    }
}
