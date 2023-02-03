using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ClipDownloader
    {
        private readonly ClipDownloadOptions downloadOptions;
        private static HttpClient httpClient = new HttpClient();

        public ClipDownloader(ClipDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
        }

        public async Task DownloadAsync(CancellationToken cancellationToken = new())
        {
            List<GqlClipTokenResponse> listLinks = await TwitchHelper.GetClipLinks(downloadOptions.Id);

            if (listLinks[0].data.clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (listLinks[0].data.clip.videoQualities is null || listLinks[0].data.clip.videoQualities.Count == 0)
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            string downloadUrl = "";

            foreach (var quality in listLinks[0].data.clip.videoQualities)
            {
                if (quality.quality + "p" + (quality.frameRate.ToString() == "30" ? "" : quality.frameRate.ToString()) == downloadOptions.Quality)
                {
                    downloadUrl = quality.sourceURL;
                }
            }

            if (downloadUrl == "")
            {
                downloadUrl = listLinks[0].data.clip.videoQualities.First().sourceURL;
            }

            downloadUrl += "?sig=" + listLinks[0].data.clip.playbackAccessToken.signature + "&token=" + HttpUtility.UrlEncode(listLinks[0].data.clip.playbackAccessToken.value);

            cancellationToken.ThrowIfCancellationRequested();

            var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(downloadOptions.Filename, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
