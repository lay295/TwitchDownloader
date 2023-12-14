using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;

namespace TwitchDownloaderCore.VideoPlatforms.Twitch.Downloaders
{
    public sealed class TwitchVideoDownloader : IVideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly IProgress<ProgressReport> _progress;
        private bool _shouldClearCache = true;

        public TwitchVideoDownloader(VideoDownloadOptions videoDownloadOptions, IProgress<ProgressReport> progress)
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

                var videoInfoResponse = await TwitchHelper.GetVideoInfo(int.Parse(downloadOptions.Id));
                if (videoInfoResponse.GqlVideoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(int.Parse(downloadOptions.Id), videoInfoResponse.GqlVideoResponse.data.video);

                var qualityPlaylist = await GetQualityPlaylist(videoInfoResponse);

                var playlistUrl = qualityPlaylist.Path;
                var baseUrl = new Uri(playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)], UriKind.Absolute);

                var videoLength = TimeSpan.FromSeconds(videoInfoResponse.GqlVideoResponse.data.video.lengthSeconds);
                DriveHelper.CheckAvailableStorageSpace(downloadOptions, qualityPlaylist.StreamInfo.Bandwidth, videoLength, _progress);

                var (playlist, videoListCrop, vodAge) = await GetVideoPlaylist(playlistUrl, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                PlatformHelper.CreateDirectory(downloadFolder);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/5]"));

                await DownloadTools.DownloadVideoPartsAsync(downloadOptions, playlist.Streams, videoListCrop, baseUrl, downloadFolder, vodAge, 2, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Verifying Parts 0% [3/5]" });

                await DownloadTools.VerifyDownloadedParts(downloadOptions, playlist.Streams, videoListCrop, baseUrl, downloadFolder, vodAge, 3, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [4/5]" });

                await DownloadTools.CombineVideoParts(downloadFolder, playlist.Streams, videoListCrop, 4, 5, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Finalizing Video 0% [5/5]" });

                var startOffsetSeconds = (double)playlist.Streams
                    .Take(videoListCrop.Start.Value)
                    .Sum(x => x.PartInfo.Duration);

                startOffsetSeconds -= downloadOptions.CropBeginningTime;
                var seekDuration = Math.Round(downloadOptions.CropEndingTime - downloadOptions.CropBeginningTime);

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                VideoInfo videoInfo = videoInfoResponse.GqlVideoResponse.data.video;
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.owner.displayName, downloadOptions.Id, videoInfo.title, videoInfo.createdAt, videoInfo.viewCount,
                    videoInfo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd(), startOffsetSeconds, videoChapterResponse.data.video.moments.edges, cancellationToken);

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

        private async Task<(M3U8 playlist, Range cropRange, double vodAge)> GetVideoPlaylist(string playlistUrl, CancellationToken cancellationToken)
        {
            var playlistString = await _httpClient.GetStringAsync(playlistUrl, cancellationToken);
            var playlist = M3U8.Parse(playlistString);

            double vodAge = 25;
            var airDateKvp = playlist.FileMetadata.UnparsedValues.FirstOrDefault(x => x.Key == "#ID3-EQUIV-TDTG:");
            if (DateTimeOffset.TryParse(airDateKvp.Value, out var airDate))
            {
                vodAge = (DateTimeOffset.UtcNow - airDate).TotalHours;
            }

            var videoListCrop = DownloadTools.GetStreamListCrop(playlist.Streams, downloadOptions);

            return (playlist, videoListCrop, vodAge);
        }

        private async Task<M3U8.Stream> GetQualityPlaylist(TwitchVideoInfo videoInfo)
        {
            var m3u8 = await TwitchHelper.GetVideoQualitiesPlaylist(videoInfo);

            for (var i = m3u8.Streams.Length - 1; i >= 0; i--)
            {
                var m3u8Stream = m3u8.Streams[i];
                if (m3u8Stream.MediaInfo.Name.StartsWith(downloadOptions.Quality, StringComparison.OrdinalIgnoreCase))
                {
                    return m3u8.Streams[i];
                }
            }

            // Unable to find specified quality, default to highest quality
            return m3u8.Streams[0];
        }
    }
}