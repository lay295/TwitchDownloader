using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class VideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly ITaskProgress _progress;
        private bool _shouldClearCache = true;

        public VideoDownloader(VideoDownloadOptions videoDownloadOptions, ITaskProgress progress = default)
        {
            downloadOptions = videoDownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
            downloadOptions.TrimBeginningTime = downloadOptions.TrimBeginningTime >= TimeSpan.Zero ? downloadOptions.TrimBeginningTime : TimeSpan.Zero;
            downloadOptions.TrimEndingTime = downloadOptions.TrimEndingTime >= TimeSpan.Zero ? downloadOptions.TrimEndingTime : TimeSpan.Zero;
            _progress = progress;
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            await TwitchHelper.CleanupAbandonedVideoCaches(downloadOptions.TempFolder, downloadOptions.CacheCleanerCallback, _progress);

            string downloadFolder = Path.Combine(
                downloadOptions.TempFolder,
                $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.Ticks}");

            _progress.SetStatus("Fetching Video Info [1/5]");

            try
            {
                ServicePointManager.DefaultConnectionLimit = downloadOptions.DownloadThreads;

                GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(downloadOptions.Id);
                if (videoInfoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(downloadOptions.Id, videoInfoResponse.data.video);

                var qualityPlaylist = await GetQualityPlaylist();

                var playlistUrl = qualityPlaylist.Path;
                var baseUrl = new Uri(playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)], UriKind.Absolute);

                var videoLength = TimeSpan.FromSeconds(videoInfoResponse.data.video.lengthSeconds);
                CheckAvailableStorageSpace(qualityPlaylist.StreamInfo.Bandwidth, videoLength);

                var (playlist, videoListCrop, airDate) = await GetVideoPlaylist(playlistUrl, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                _progress.SetTemplateStatus("Downloading {0}% [2/5]", 0);

                await DownloadVideoPartsAsync(playlist.Streams, videoListCrop, baseUrl, downloadFolder, airDate, cancellationToken);

                _progress.SetTemplateStatus("Verifying Parts {0}% [3/5]", 0);

                await VerifyDownloadedParts(playlist.Streams, videoListCrop, baseUrl, downloadFolder, airDate, cancellationToken);

                _progress.SetTemplateStatus("Combining Parts {0}% [4/5]", 0);

                await CombineVideoParts(downloadFolder, playlist.Streams, videoListCrop, cancellationToken);

                _progress.SetTemplateStatus("Finalizing Video {0}% [5/5]", 0);

                var startOffset = TimeSpan.FromSeconds((double)playlist.Streams
                    .Take(videoListCrop.Start.Value)
                    .Sum(x => x.PartInfo.Duration));

                startOffset = downloadOptions.TrimBeginningTime - startOffset;
                var seekDuration = downloadOptions.TrimEndingTime - downloadOptions.TrimBeginningTime;

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                VideoInfo videoInfo = videoInfoResponse.data.video;
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.owner.displayName, downloadOptions.Id.ToString(), videoInfo.title, videoInfo.createdAt, videoInfo.viewCount,
                    videoInfo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd(), downloadOptions.TrimBeginningTime,
                    videoChapterResponse.data.video.moments.edges, cancellationToken);

                var finalizedFileDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
                if (!finalizedFileDirectory.Exists)
                {
                    TwitchHelper.CreateDirectory(finalizedFileDirectory.FullName);
                }

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await Task.Run(() => RunFfmpegVideoCopy(downloadFolder, metadataPath, startOffset, seekDuration > TimeSpan.Zero ? seekDuration : videoLength), cancellationToken);
                    if (ffmpegExitCode != 0)
                    {
                        _progress.LogError($"Failed to finalize video (code {ffmpegExitCode}), retrying in 10 seconds...");
                        await Task.Delay(10_000, cancellationToken);
                    }
                } while (ffmpegExitCode != 0 && ffmpegRetries++ < 1);

                if (ffmpegExitCode != 0 || !File.Exists(downloadOptions.Filename))
                {
                    _shouldClearCache = false;
                    throw new Exception($"Failed to finalize video. The download cache has not been cleared and can be found at {downloadFolder} along with a log file.");
                }

                _progress.ReportProgress(100);
            }
            finally
            {
                if (_shouldClearCache)
                {
                    Cleanup(downloadFolder);
                }
            }
        }

        private void CheckAvailableStorageSpace(int bandwidth, TimeSpan videoLength)
        {
            var videoSizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth,
                downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : videoLength);
            var tempFolderDrive = DriveHelper.GetOutputDrive(downloadOptions.TempFolder);
            var destinationDrive = DriveHelper.GetOutputDrive(downloadOptions.Filename);

            if (tempFolderDrive.Name == destinationDrive.Name)
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes * 2)
                {
                    _progress.LogWarning($"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download.");
                }
            }
            else
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    // More drive space is needed by the raw ts files due to repeat metadata, but the amount of metadata packets can vary between files so we won't bother.
                    _progress.LogWarning($"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download.");
                }

                if (destinationDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    _progress.LogWarning($"The drive '{destinationDrive.Name}' may not have enough free space to complete finalization.");
                }
            }
        }

        private async Task DownloadVideoPartsAsync(IEnumerable<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, DateTimeOffset vodAirDate, CancellationToken cancellationToken)
        {
            var partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            var videoPartsQueue = new ConcurrentQueue<string>(playlist.Take(videoListCrop).Select(x => x.Path));

            var downloadThreads = new VideoDownloadThread[downloadOptions.DownloadThreads];
            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadThreads[i] = new VideoDownloadThread(videoPartsQueue, _httpClient, baseUrl, downloadFolder, vodAirDate, downloadOptions.ThrottleKib, _progress, cancellationToken);
            }

            var downloadExceptions = await WaitForDownloadThreads(downloadThreads, videoPartsQueue, partCount, cancellationToken);

            LogDownloadThreadExceptions(downloadExceptions);
        }

        private async Task<IReadOnlyCollection<Exception>> WaitForDownloadThreads(VideoDownloadThread[] downloadThreads, ConcurrentQueue<string> videoPartsQueue, int partCount, CancellationToken cancellationToken)
        {
            var allThreadsExited = false;
            var previousDoneCount = 0;
            var restartedThreads = 0;
            var maxRestartedThreads = (int)Math.Max(downloadOptions.DownloadThreads * 1.5, 10);
            var downloadExceptions = new Dictionary<int, Exception>();
            do
            {
                if (videoPartsQueue.Count != previousDoneCount)
                {
                    previousDoneCount = videoPartsQueue.Count;
                    var percent = (int)((partCount - previousDoneCount) / (double)partCount * 100);
                    _progress.ReportProgress(percent);
                }

                allThreadsExited = true;
                foreach (var thread in downloadThreads)
                {
                    var task = thread.ThreadTask;

                    if (task.IsFaulted)
                    {
                        downloadExceptions.TryAdd(task.Id, task.Exception);

                        if (restartedThreads <= maxRestartedThreads)
                        {
                            thread.StartDownload();
                            restartedThreads++;
                        }
                    }

                    if (allThreadsExited && !task.IsCompleted)
                    {
                        allThreadsExited = false;
                    }
                }

                await Task.Delay(100, cancellationToken);
            } while (!allThreadsExited);

            _progress.ReportProgress(100);

            if (restartedThreads == maxRestartedThreads)
            {
                throw new AggregateException("The download thread restart limit was reached.", downloadExceptions.Values);
            }

            return downloadExceptions.Values;
        }

        private void LogDownloadThreadExceptions(IReadOnlyCollection<Exception> downloadExceptions)
        {
            if (downloadExceptions.Count == 0)
                return;

            var culprits = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (var downloadException in downloadExceptions)
            {
                var ex = downloadException switch
                {
                    AggregateException { InnerException: { } innerException } => innerException,
                    _ => downloadException
                };

                // Try to only log exceptions from different sources
                var targetSiteName = ex.TargetSite?.Name ?? "PrivateMethod";
                var targetSiteTypeName = ex.TargetSite?.DeclaringType?.Name ?? "PrivateType";

                var culprit = $"{targetSiteTypeName}.{targetSiteName}";
                if (!culprits.Add(culprit))
                    continue;

                sb.Append(", ");
                sb.Append(ex.Message);
                sb.Append(" at ");
                sb.Append(culprit);
            }

            if (sb.Length == 0)
            {
                return;
            }

            _ = downloadExceptions.Count == 1
                ? sb.Replace(",", "1 error was encountered while downloading:", 0, 1)
                : sb.Replace(",", $"{downloadExceptions.Count} errors were encountered while downloading:", 0, 1);
            _progress.LogInfo(sb.ToString());
        }

        private async Task VerifyDownloadedParts(ICollection<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, DateTimeOffset vodAirDate, CancellationToken cancellationToken)
        {
            var failCounts = new Dictionary<TsVerifyResult, int>();
            var failedParts = new List<M3U8.Stream>();
            var partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            var doneCount = 0;

            foreach (var stream in playlist.Take(videoListCrop))
            {
                var filePath = Path.Combine(downloadFolder, DownloadTools.RemoveQueryString(stream.Path));

                var result = await DownloadTools.VerifyTransportStream(filePath, _progress);
                if (result != TsVerifyResult.Success)
                {
                    CollectionsMarshal.GetValueRefOrAddDefault(failCounts, result, out _)++; // Gets or creates the value and increments it
                    failedParts.Add(stream);
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failCounts.Count > 0)
            {
                _progress.LogVerbose($"Verification results: {string.Join(", ", failCounts.Select(x => $"{x.Key}: {x.Value}"))}");
            }

            if (failedParts.Count > 0)
            {
                if (partCount > 20 && failedParts.Count >= partCount * 0.95)
                {
                    // 19/20 parts failed to verify. Either the VOD is heavily corrupted or something went horribly wrong.
                    // TODO: Somehow let the user bypass this. Maybe with callbacks?
                    throw new Exception($"{failedParts.Count}/{partCount} parts failed to verify for the following reasons: "
                                        + string.Join(", ", failCounts.Where(x => x.Value > 0).Select(x => $"{x.Key}: {x.Value}"))
                                        + ", aborting.");
                }

                _progress.LogInfo($"The following parts will be redownloaded: {string.Join(", ", failedParts)}");
                await DownloadVideoPartsAsync(failedParts, Range.All, baseUrl, downloadFolder, vodAirDate, cancellationToken);
            }
        }

        private int RunFfmpegVideoCopy(string downloadFolder, string metadataPath, TimeSpan startOffset, TimeSpan seekDuration)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = downloadOptions.FfmpegPath,
                    Arguments = string.Format(
                        "-hide_banner -stats -y -avoid_negative_ts make_zero " + (downloadOptions.TrimBeginning ? "-ss {2} " : "") + "-i \"{0}\" -i \"{1}\" -map_metadata 1 -analyzeduration {3} -probesize {3} " + (downloadOptions.TrimEnding ? "-t {4} " : "") + "-c:v copy \"{5}\"",
                        Path.Combine(downloadFolder, "output.ts"), metadataPath, startOffset.TotalSeconds.ToString(CultureInfo.InvariantCulture), int.MaxValue, seekDuration.TotalSeconds.ToString(CultureInfo.InvariantCulture), Path.GetFullPath(downloadOptions.Filename)),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            var encodingTimeRegex = new Regex(@"(?<=time=)(\d\d):(\d\d):(\d\d)\.(\d\d)", RegexOptions.Compiled);
            var logQueue = new ConcurrentQueue<string>();

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is null)
                    return;

                logQueue.Enqueue(e.Data); // We cannot use -report ffmpeg arg because it redirects stderr

                HandleFfmpegOutput(e.Data, encodingTimeRegex, seekDuration);
            };

            process.Start();
            process.BeginErrorReadLine();

            using var logWriter = File.AppendText(Path.Combine(downloadFolder, "ffmpegLog.txt"));
            do // We cannot handle logging inside the ErrorDataReceived lambda because more than 1 can come in at once and cause a race condition. lay295#598
            {
                Thread.Sleep(100);
                while (!logQueue.IsEmpty && logQueue.TryDequeue(out var logMessage))
                {
                    logWriter.WriteLine(logMessage);
                }
            } while (!process.HasExited || !logQueue.IsEmpty);

            return process.ExitCode;
        }

        private void HandleFfmpegOutput(string output, Regex encodingTimeRegex, TimeSpan videoLength)
        {
            var encodingTimeMatch = encodingTimeRegex.Match(output);
            if (!encodingTimeMatch.Success)
                return;

            // TimeSpan.Parse insists that hours cannot be greater than 24, thus we must use the TimeSpan ctor.
            if (!int.TryParse(encodingTimeMatch.Groups[1].ValueSpan, out var hours)) return;
            if (!int.TryParse(encodingTimeMatch.Groups[2].ValueSpan, out var minutes)) return;
            if (!int.TryParse(encodingTimeMatch.Groups[3].ValueSpan, out var seconds)) return;
            if (!int.TryParse(encodingTimeMatch.Groups[4].ValueSpan, out var milliseconds)) return;
            var encodingTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);

            var percent = (int)Math.Round(encodingTime / videoLength * 100);

            _progress.ReportProgress(Math.Clamp(percent, 0, 100));
        }

        private async Task<(M3U8 playlist, Range cropRange, DateTimeOffset airDate)> GetVideoPlaylist(string playlistUrl, CancellationToken cancellationToken)
        {
            var playlistString = await _httpClient.GetStringAsync(playlistUrl, cancellationToken);
            var playlist = M3U8.Parse(playlistString);

            var airDate = DateTimeOffset.UtcNow.AddHours(-25);
            var airDateKvp = playlist.FileMetadata.UnparsedValues.FirstOrDefault(x => x.Key == "#ID3-EQUIV-TDTG:");
            if (DateTimeOffset.TryParse(airDateKvp.Value, out var vodAirDate))
            {
                airDate = vodAirDate;
            }

            var videoListCrop = GetStreamListCrop(playlist.Streams, downloadOptions);

            return (playlist, videoListCrop, airDate);
        }

        private static Range GetStreamListCrop(IList<M3U8.Stream> streamList, VideoDownloadOptions downloadOptions)
        {
            var startIndex = 0;
            if (downloadOptions.TrimBeginning)
            {
                var startTime = 0m;
                var cropTotalSeconds = (decimal)downloadOptions.TrimBeginningTime.TotalSeconds;
                foreach (var videoPart in streamList)
                {
                    if (startTime + videoPart.PartInfo.Duration > cropTotalSeconds)
                        break;

                    startIndex++;
                    startTime += videoPart.PartInfo.Duration;
                }
            }

            var endIndex = streamList.Count;
            if (downloadOptions.TrimEnding)
            {
                var endTime = streamList.Sum(x => x.PartInfo.Duration);
                var cropTotalSeconds = (decimal)downloadOptions.TrimEndingTime.TotalSeconds;
                for (var i = streamList.Count - 1; i >= 0; i--)
                {
                    if (endTime - streamList[i].PartInfo.Duration < cropTotalSeconds)
                        break;

                    endIndex--;
                    endTime -= streamList[i].PartInfo.Duration;
                }
            }

            return new Range(startIndex, endIndex);
        }

        private async Task<M3U8.Stream> GetQualityPlaylist()
        {
            GqlVideoTokenResponse accessToken = await TwitchHelper.GetVideoToken(downloadOptions.Id, downloadOptions.Oauth);

            if (accessToken.data.videoPlaybackAccessToken is null)
            {
                throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
            }

            var playlistString = await TwitchHelper.GetVideoPlaylist(downloadOptions.Id, accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements"))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            var m3u8 = M3U8.Parse(playlistString);

            return m3u8.GetStreamOfQuality(downloadOptions.Quality);
        }

        private async Task CombineVideoParts(string downloadFolder, IEnumerable<M3U8.Stream> playlist, Range videoListCrop, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(downloadFolder);
            string outputFile = Path.Combine(downloadFolder, "output.ts");

            int partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            int doneCount = 0;

            await using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            foreach (var part in playlist.Take(videoListCrop))
            {
                await DriveHelper.WaitForDrive(outputDrive, _progress, cancellationToken);

                string partFile = Path.Combine(downloadFolder, DownloadTools.RemoveQueryString(part.Path));
                if (File.Exists(partFile))
                {
                    await using (var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await fs.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                    }

                    try
                    {
                        File.Delete(partFile);
                    }
                    catch { /* If we can't delete, oh well. It should get cleanup up later anyways */ }
                }

                doneCount++;
                int percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static void Cleanup(string downloadFolder)
        {
            try
            {
                if (Directory.Exists(downloadFolder))
                {
                    Directory.Delete(downloadFolder, true);
                }
            }
            catch (IOException) { } // Directory is probably being used by another process
        }
    }
}