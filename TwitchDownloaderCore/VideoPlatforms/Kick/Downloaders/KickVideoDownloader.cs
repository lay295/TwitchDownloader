using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch;

namespace TwitchDownloaderCore.VideoPlatforms.Kick.Downloaders
{
    public class KickVideoDownloader : IVideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly IProgress<ProgressReport> _progress;
        private bool _shouldClearCache = true;

        public KickVideoDownloader(VideoDownloadOptions videoDownloadOptions, IProgress<ProgressReport> progress)
        {
            downloadOptions = videoDownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
            _progress = progress;
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            TwitchHelper.CleanupUnmanagedCacheFiles(downloadOptions.TempFolder, _progress);

            string downloadFolder = Path.Combine(
                downloadOptions.TempFolder,
                $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

            _progress.Report(new ProgressReport(ReportType.SameLineStatus, "Fetching Video Info [1/5]"));

            try
            {
                ServicePointManager.DefaultConnectionLimit = downloadOptions.DownloadThreads;
                KickVideoResponse videoInfo = await KickHelper.GetVideoInfo(downloadOptions.Id);
                var (qualitiesPlaylist, desiredStream) = await GetQualitiesPlaylist(videoInfo);
                var selectedStream = qualitiesPlaylist.Streams[desiredStream];

                var playlistUrl = videoInfo.source[..(videoInfo.source.LastIndexOf('/') + 1)] + selectedStream.Path;
                var baseUrl = new Uri(playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)], UriKind.Absolute);

                var videoLength = TimeSpan.FromSeconds(videoInfo.Duration);
                DriveHelper.CheckAvailableStorageSpace(downloadOptions, selectedStream.StreamInfo.Bandwidth, videoLength, _progress);

                var (playlist, videoListCrop) = await GetVideoPlaylist(playlistUrl, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                PlatformHelper.CreateDirectory(downloadFolder);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/5]"));

                await DownloadTools.DownloadVideoPartsAsync(downloadOptions, playlist.Streams, videoListCrop, baseUrl, downloadFolder, int.MaxValue, 2, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Verifying Parts 0% [3/5]" });

                await DownloadTools.VerifyDownloadedParts(downloadOptions, playlist.Streams, videoListCrop, baseUrl, downloadFolder, int.MaxValue, 3, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [4/5]" });

                await DownloadTools.CombineVideoParts(downloadFolder, playlist.Streams, videoListCrop, 4, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Finalizing Video 0% [5/5]" });

                var startOffsetSeconds = (double)playlist.Streams
                    .Take(videoListCrop.Start.Value)
                    .Sum(x => x.PartInfo.Duration);

                startOffsetSeconds -= downloadOptions.CropBeginningTime;
                var seekDuration = Math.Round(downloadOptions.CropEndingTime - downloadOptions.CropBeginningTime);

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.StreamerName, downloadOptions.Id, videoInfo.Title,
                    videoInfo.CreatedAt, videoInfo.ViewCount, null, startOffsetSeconds, null, cancellationToken);

                var finalizedFileDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
                if (!finalizedFileDirectory.Exists)
                {
                    PlatformHelper.CreateDirectory(finalizedFileDirectory.FullName);
                }

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await Task.Run(() => DownloadTools.RunFfmpegVideoCopy(downloadOptions, downloadFolder, metadataPath, startOffsetSeconds, seekDuration, _progress), cancellationToken);
                    if (ffmpegExitCode != 0)
                    {
                        _progress.Report(new ProgressReport(ReportType.Log, $"Failed to finalize video (code {ffmpegExitCode}), retrying in 10 seconds..."));
                        await Task.Delay(10_000, cancellationToken);
                    }
                } while (ffmpegExitCode != 0 && ffmpegRetries++ < 1);

                if (ffmpegExitCode != 0 || !File.Exists(downloadOptions.Filename))
                {
                    _shouldClearCache = false;
                    throw new Exception($"Failed to finalize video. The download cache has not been cleared and can be found at {downloadFolder} along with a log file.");
                }

                _progress.Report(new ProgressReport(ReportType.SameLineStatus, "Finalizing Video 100% [5/5]"));
                _progress.Report(new ProgressReport(100));
            }
            finally
            {
                if (_shouldClearCache)
                {
                    await Task.Delay(300, CancellationToken.None); // Wait a bit for any download threads to be killed.
                    DownloadTools.Cleanup(downloadFolder);
                }
            }
        }

        private async Task<(M3U8 playlist, Range cropRange)> GetVideoPlaylist(string playlistUrl, CancellationToken cancellationToken)
        {
            var playlistString = await Task.Run(() => CurlImpersonate.GetCurlResponse(playlistUrl), cancellationToken);
            var playlist = M3U8.Parse(playlistString);

            var videoListCrop = DownloadTools.GetStreamListCrop(playlist.Streams, downloadOptions);

            return (playlist, videoListCrop);
        }

        private async Task<(M3U8 playlist, Index streamIndex)> GetQualitiesPlaylist(KickVideoResponse videoInfo)
        {
            var m3u8 = await KickHelper.GetQualitiesPlaylist(videoInfo);

            for (var i = m3u8.Streams.Length - 1; i >= 0; i--)
            {
                var m3u8Stream = m3u8.Streams[i];
                if (m3u8Stream.MediaInfo.Name.StartsWith(downloadOptions.Quality))
                {
                    return (m3u8, i);
                }
            }

            // Unable to find specified quality, default to highest quality
            return (m3u8, 0);
        }
    }
}
