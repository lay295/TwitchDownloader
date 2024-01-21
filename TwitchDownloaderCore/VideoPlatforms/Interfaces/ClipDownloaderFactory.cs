using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Kick.Downloaders;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Downloaders;

namespace TwitchDownloaderCore.VideoPlatforms.Interfaces
{
    public class ClipDownloaderFactory
    {
        public Progress<ProgressReport> _progress { get; }

        public ClipDownloaderFactory(Progress<ProgressReport> progress)
        {
            _progress = progress;
        }

        public IClipDownloader Create(ClipDownloadOptions downloadOptions)
        {
            if (downloadOptions.VideoPlatform == VideoPlatform.Twitch)
            {
                return new TwitchClipDownloader(downloadOptions, _progress);
            }

            if (downloadOptions.VideoPlatform == VideoPlatform.Kick)
            {
                return new KickClipDownloader(downloadOptions, _progress);
            }

            throw new NotImplementedException();
        }
    }
}
