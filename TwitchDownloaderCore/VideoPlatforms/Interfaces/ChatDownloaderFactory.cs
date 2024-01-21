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
    public class ChatDownloaderFactory
    {
        public Progress<ProgressReport> _progress { get; }

        public ChatDownloaderFactory(Progress<ProgressReport> progress)
        {
            _progress = progress;
        }

        public IChatDownloader Create(ChatDownloadOptions downloadOptions)
        {
            if (downloadOptions.VideoPlatform == VideoPlatform.Twitch)
            {
                return new TwitchChatDownloader(downloadOptions, _progress);
            }

            if (downloadOptions.VideoPlatform == VideoPlatform.Kick)
            {
                return new KickChatDownloader(downloadOptions, _progress);
            }

            throw new NotImplementedException();
        }
    }
}
