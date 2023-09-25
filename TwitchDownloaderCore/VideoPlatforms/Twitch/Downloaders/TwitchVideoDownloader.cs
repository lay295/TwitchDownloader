using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
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

                GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(int.Parse(downloadOptions.Id));
                if (videoInfoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetVideoChapters(int.Parse(downloadOptions.Id));

                var (playlistUrl, bandwidth) = await GetPlaylistUrl();
                string baseUrl = playlistUrl.Substring(0, playlistUrl.LastIndexOf('/') + 1);

                var videoLength = TimeSpan.FromSeconds(videoInfoResponse.data.video.lengthSeconds);
                DriveHelper.CheckAvailableStorageSpace(downloadOptions, bandwidth, videoLength, _progress);

                List<KeyValuePair<string, double>> videoList = new List<KeyValuePair<string, double>>();
                (List<string> videoPartsList, double vodAge) = await GetVideoPartsList(playlistUrl, videoList, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/5]"));

                await DownloadTools.DownloadVideoPartsAsync(downloadOptions, videoPartsList, baseUrl, downloadFolder, vodAge, _progress, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Verifying Parts 0% [3/5]" });

                await DownloadTools.VerifyDownloadedParts(downloadOptions, videoPartsList, baseUrl, downloadFolder, vodAge, _progress, cancellationToken);

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
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfoResponse.data.video.owner.displayName, downloadOptions.Id.ToString(), videoInfoResponse.data.video.title,
                    videoInfoResponse.data.video.createdAt, videoInfoResponse.data.video.viewCount, startOffset, videoChapterResponse.data.video.moments.edges, cancellationToken);

                var finalizedFileDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
                if (!finalizedFileDirectory.Exists)
                {
                    TwitchHelper.CreateDirectory(finalizedFileDirectory.FullName);
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

        private async Task<(List<string> videoParts, double vodAge)> GetVideoPartsList(string playlistUrl, List<KeyValuePair<string, double>> videoList, CancellationToken cancellationToken)
        {
            string[] videoChunks = (await _httpClient.GetStringAsync(playlistUrl, cancellationToken)).Split('\n');

            double vodAge = 25;

            try
            {
                vodAge = (DateTimeOffset.UtcNow - DateTimeOffset.Parse(videoChunks.First(x => x.StartsWith("#ID3-EQUIV-TDTG:")).Replace("#ID3-EQUIV-TDTG:", ""))).TotalHours;
            }
            catch { }

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

            return (videoParts, vodAge);
        }

        private async Task<(string url, int bandwidth)> GetPlaylistUrl()
        {
            GqlVideoTokenResponse accessToken = await TwitchHelper.GetVideoToken(int.Parse(downloadOptions.Id), downloadOptions.Oauth);

            if (accessToken.data.videoPlaybackAccessToken is null)
            {
                throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
            }

            string[] videoPlaylist = await TwitchHelper.GetVideoPlaylist(int.Parse(downloadOptions.Id), accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (videoPlaylist[0].Contains("vod_manifest_restricted"))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            var videoQualities = new List<KeyValuePair<string, (string, int)>>();

            for (int i = 0; i < videoPlaylist.Length; i++)
            {
                if (videoPlaylist[i].Contains("#EXT-X-MEDIA"))
                {
                    string lastPart = videoPlaylist[i].Substring(videoPlaylist[i].IndexOf("NAME=\"") + 6);
                    string stringQuality = lastPart.Substring(0, lastPart.IndexOf('"'));

                    var bandwidthStartIndex = videoPlaylist[i + 1].IndexOf("BANDWIDTH=") + 10;
                    var bandwidthEndIndex = videoPlaylist[i + 1].IndexOf(',') - bandwidthStartIndex;
                    int.TryParse(videoPlaylist[i + 1].Substring(bandwidthStartIndex, bandwidthEndIndex), out var bandwidth);

                    if (!videoQualities.Any(x => x.Key.Equals(stringQuality)))
                    {
                        videoQualities.Add(new KeyValuePair<string, (string, int)>(stringQuality, (videoPlaylist[i + 2], bandwidth)));
                    }
                }
            }

            if (downloadOptions.Quality != null && videoQualities.Any(x => x.Key.StartsWith(downloadOptions.Quality)))
            {
                return videoQualities.Last(x => x.Key.StartsWith(downloadOptions.Quality)).Value;
            }

            // Unable to find specified quality, defaulting to highest quality
            return videoQualities.First().Value;
        }
    }
}