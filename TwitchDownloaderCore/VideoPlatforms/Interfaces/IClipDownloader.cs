using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.VideoPlatforms.Interfaces
{
    public interface IClipDownloader
    {
        Task DownloadAsync(CancellationToken cancellationToken);
    }
}