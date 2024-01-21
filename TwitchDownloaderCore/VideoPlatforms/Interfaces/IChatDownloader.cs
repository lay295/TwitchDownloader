using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.VideoPlatforms.Interfaces
{
    public interface IChatDownloader
    {
        Task DownloadAsync(CancellationToken cancellationToken);
    }
}
