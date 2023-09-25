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
    public class VideoDownloaderFactory
    {
        public Progress<ProgressReport> _progress { get; }

        public VideoDownloaderFactory(Progress<ProgressReport> progress)
        {
            _progress = progress;
        }

        public IVideoDownloader Create(VideoDownloadOptions downloadOptions)
        {
            if (downloadOptions.VideoPlatform == VideoPlatform.Twitch)
            {
                return new TwitchVideoDownloader(downloadOptions, _progress);
            }

            if (downloadOptions.VideoPlatform == VideoPlatform.Kick)
            {
                return new KickVideoDownloader(downloadOptions, _progress);
            }

            throw new NotImplementedException();
        }
    }
}
