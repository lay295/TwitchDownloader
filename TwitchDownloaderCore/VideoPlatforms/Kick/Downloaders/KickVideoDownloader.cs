using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;

namespace TwitchDownloaderCore.VideoPlatforms.Kick.Downloaders
{
    public class KickVideoDownloader : IVideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
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
                IVideoInfo videoInfo = await KickHelper.GetVideoInfo(downloadOptions.Id);
                var (playlistUrl, bandwidth) = GetPlaylistUrl(videoInfo);
                var baseUrl = new Uri(playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)]);

                var videoLength = TimeSpan.FromSeconds(videoInfo.Duration);
                DriveHelper.CheckAvailableStorageSpace(downloadOptions, bandwidth, videoLength, _progress);

                List<KeyValuePair<string, double>> videoList = new List<KeyValuePair<string, double>>();
                List<string> videoPartsList = await GetVideoPartsList(playlistUrl, videoList, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                PlatformHelper.CreateDirectory(downloadFolder);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/5]"));

                await DownloadTools.DownloadVideoPartsAsync(downloadOptions, videoPartsList, baseUrl, downloadFolder, 0, 2, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Verifying Parts 0% [3/5]" });

                await DownloadTools.VerifyDownloadedParts(downloadOptions, videoPartsList, baseUrl, downloadFolder, 0, 3, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [4/5]" });

                await DownloadTools.CombineVideoParts(downloadFolder, videoPartsList, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Finalizing Video 0% [5/5]" });

                double startOffset = 0.0;

                for (int i = 0; i < videoList.Count; i++)
                {
                    if (videoList[i].Key == videoPartsList[0])
                        break;

                    startOffset += videoList[i].Value;
                }

                double seekTime = downloadOptions.CropBeginningTime;
                double seekDuration = Math.Round(downloadOptions.CropEndingTime - seekTime);

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.StreamerName, downloadOptions.Id, videoInfo.Title,
                    videoInfo.CreatedAt, videoInfo.ViewCount, null, startOffset, null, cancellationToken);

                var finalizedFileDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
                if (!finalizedFileDirectory.Exists)
                {
                    PlatformHelper.CreateDirectory(finalizedFileDirectory.FullName);
                }

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await Task.Run(() => DownloadTools.RunFfmpegVideoCopy(downloadOptions, downloadFolder, metadataPath, seekTime, startOffset, seekDuration, _progress), cancellationToken);
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
                    DownloadTools.Cleanup(downloadFolder);
                }
            }
        }

        private async Task<List<string>> GetVideoPartsList(string playlistUrl, List<KeyValuePair<string, double>> videoList, CancellationToken cancellationToken)
        {
            string[] videoChunks = (await _httpClient.GetStringAsync(playlistUrl, cancellationToken)).Split('\n');

            for (int i = 0; i < videoChunks.Length; i++)
            {
                if (videoChunks[i].StartsWith("#EXTINF"))
                {
                    if (videoChunks[i + 1].StartsWith("#EXT-X-BYTERANGE"))
                    {
                        if (videoList.Any(x => x.Key == videoChunks[i + 2]))
                        {
                            KeyValuePair<string, double> pair = videoList.Where(x => x.Key == videoChunks[i + 2]).First();
                            pair = new KeyValuePair<string, double>(pair.Key, pair.Value + double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 2], double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture)));
                        }
                    }
                    else
                    {
                        videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 1], double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture)));
                    }
                }
            }

            List<KeyValuePair<string, double>> videoListCropped = DownloadTools.GenerateCroppedVideoList(videoList, downloadOptions);

            List<string> videoParts = new List<string>(videoListCropped.Count);
            foreach (var part in videoListCropped)
            {
                videoParts.Add(part.Key);
            }

            return videoParts;
        }

        private (string url, int bandwidth) GetPlaylistUrl(IVideoInfo videoInfo)
        {
            if (downloadOptions.Quality != null && videoInfo.VideoQualities.Any(x => x.Quality.StartsWith(downloadOptions.Quality)))
            {
                VideoQuality quality = videoInfo.VideoQualities.Last(x => x.Quality.StartsWith(downloadOptions.Quality));
                return (quality.SourceUrl, quality.Bandwidth);
            }

            // Unable to find specified quality, defaulting to highest quality
            VideoQuality firstQuality = videoInfo.VideoQualities.First();
            return (firstQuality.SourceUrl, firstQuality.Bandwidth);
        }
    }
}
