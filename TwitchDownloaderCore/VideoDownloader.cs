﻿using System;
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
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class VideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly IProgress<ProgressReport> _progress;
        private bool _shouldClearCache = true;

        public VideoDownloader(VideoDownloadOptions videoDownloadOptions, IProgress<ProgressReport> progress)
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

                var (playlist, videoListCrop, vodAge) = await GetVideoPlaylist(playlistUrl, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/5]"));

                await DownloadVideoPartsAsync(playlist.Streams, videoListCrop, baseUrl, downloadFolder, vodAge, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Verifying Parts 0% [3/5]" });

                await VerifyDownloadedParts(playlist.Streams, videoListCrop, baseUrl, downloadFolder, vodAge, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [4/5]" });

                await CombineVideoParts(downloadFolder, playlist.Streams, videoListCrop, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Finalizing Video 0% [5/5]" });

                var startOffsetSeconds = (double)playlist.Streams
                    .Take(videoListCrop.Start.Value)
                    .Sum(x => x.PartInfo.Duration);

                startOffsetSeconds = downloadOptions.CropBeginningTime - startOffsetSeconds;
                double seekDuration = Math.Round(downloadOptions.CropEndingTime - downloadOptions.CropBeginningTime);

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                VideoInfo videoInfo = videoInfoResponse.data.video;
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.owner.displayName, downloadOptions.Id.ToString(), videoInfo.title, videoInfo.createdAt, videoInfo.viewCount,
                    videoInfo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd(), startOffsetSeconds, videoChapterResponse.data.video.moments.edges, cancellationToken);

                var finalizedFileDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
                if (!finalizedFileDirectory.Exists)
                {
                    TwitchHelper.CreateDirectory(finalizedFileDirectory.FullName);
                }

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await Task.Run(() => RunFfmpegVideoCopy(downloadFolder, metadataPath, startOffsetSeconds, seekDuration), cancellationToken);
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
                    Cleanup(downloadFolder);
                }
            }
        }

        private void CheckAvailableStorageSpace(int bandwidth, TimeSpan videoLength)
        {
            var videoSizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth,
                downloadOptions.CropBeginning ? TimeSpan.FromSeconds(downloadOptions.CropBeginningTime) : TimeSpan.Zero,
                downloadOptions.CropEnding ? TimeSpan.FromSeconds(downloadOptions.CropEndingTime) : videoLength);
            var tempFolderDrive = DriveHelper.GetOutputDrive(downloadOptions.TempFolder);
            var destinationDrive = DriveHelper.GetOutputDrive(downloadOptions.Filename);

            if (tempFolderDrive.Name == destinationDrive.Name)
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes * 2)
                {
                    _progress.Report(new ProgressReport(ReportType.Log, $"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download."));
                }
            }
            else
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    // More drive space is needed by the raw ts files due to repeat metadata, but the amount of metadata packets can vary between files so we won't bother.
                    _progress.Report(new ProgressReport(ReportType.Log, $"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download."));
                }

                if (destinationDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    _progress.Report(new ProgressReport(ReportType.Log, $"The drive '{destinationDrive.Name}' may not have enough free space to complete finalization."));
                }
            }
        }

        private async Task DownloadVideoPartsAsync(IEnumerable<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            var partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            var videoPartsQueue = new ConcurrentQueue<string>(playlist.Take(videoListCrop).Select(x => x.Path));
            var downloadTasks = new Task[downloadOptions.DownloadThreads];

            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadTasks[i] = StartNewDownloadThread(videoPartsQueue, baseUrl, downloadFolder, vodAge, cancellationToken);
            }

            var downloadExceptions = await WaitForDownloadThreads(downloadTasks, videoPartsQueue, baseUrl, downloadFolder, vodAge, partCount, cancellationToken);

            LogDownloadThreadExceptions(downloadExceptions);
        }

        private Task StartNewDownloadThread(ConcurrentQueue<string> videoPartsQueue, Uri baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                ExecuteDownloadThread,
                new Tuple<ConcurrentQueue<string>, HttpClient, Uri, string, double, int, CancellationToken>(
                    videoPartsQueue, _httpClient, baseUrl, downloadFolder, vodAge, downloadOptions.ThrottleKib, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);

            static void ExecuteDownloadThread(object state)
            {
                var (partQueue, httpClient, rootUrl, cacheFolder, videoAge, throttleKib, cancelToken) =
                    (Tuple<ConcurrentQueue<string>, HttpClient, Uri, string, double, int, CancellationToken>)state;

                using var cts = new CancellationTokenSource();
                cancelToken.Register(PropagateCancel, cts);

                while (!partQueue.IsEmpty)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    string videoPart = null;
                    try
                    {
                        if (partQueue.TryDequeue(out videoPart))
                        {
                            DownloadVideoPartAsync(httpClient, rootUrl, videoPart, cacheFolder, videoAge, throttleKib, cts).GetAwaiter().GetResult();
                        }
                    }
                    catch
                    {
                        if (videoPart != null && !cancelToken.IsCancellationRequested)
                        {
                            // Requeue the video part now instead of deferring to the verifier since we already know it's bad
                            partQueue.Enqueue(videoPart);
                        }

                        throw;
                    }

                    const int A_PRIME_NUMBER = 71;
                    Thread.Sleep(A_PRIME_NUMBER);
                }
            }

            static void PropagateCancel(object tokenSourceToCancel)
            {
                try
                {
                    ((CancellationTokenSource)tokenSourceToCancel)?.Cancel();
                }
                catch (ObjectDisposedException) { }
            }
        }

        private async Task<IReadOnlyCollection<Exception>> WaitForDownloadThreads(Task[] tasks, ConcurrentQueue<string> videoPartsQueue, Uri baseUrl, string downloadFolder, double vodAge, int partCount, CancellationToken cancellationToken)
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
                    _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Downloading {percent}% [2/5]"));
                    _progress.Report(new ProgressReport(percent));
                }

                allThreadsExited = true;
                for (var t = 0; t < tasks.Length; t++)
                {
                    var task = tasks[t];

                    if (task.IsFaulted)
                    {
                        downloadExceptions.TryAdd(task.Id, task.Exception);

                        if (restartedThreads <= maxRestartedThreads)
                        {
                            tasks[t] = StartNewDownloadThread(videoPartsQueue, baseUrl, downloadFolder, vodAge, cancellationToken);
                            restartedThreads++;
                        }
                    }

                    if (allThreadsExited && !task.IsCompleted)
                    {
                        allThreadsExited = false;
                    }
                }

                await Task.Delay(300, cancellationToken);
            } while (!allThreadsExited);

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

            var culpritList = new List<string>();
            var sb = new StringBuilder();
            foreach (var downloadException in downloadExceptions)
            {
                var ex = downloadException switch
                {
                    AggregateException { InnerException: { } innerException } => innerException,
                    _ => downloadException
                };

                // Try to only log exceptions from different sources
                var culprit = ex.TargetSite?.Name;
                if (string.IsNullOrEmpty(culprit) || culpritList.Contains(culprit))
                    continue;

                sb.EnsureCapacity(sb.Capacity + ex.Message.Length + culprit.Length + 6);
                sb.Append(", ");
                sb.Append(ex.Message);
                sb.Append(" at ");
                sb.Append(culprit);
                culpritList.Add(culprit);
            }

            if (sb.Length == 0)
            {
                return;
            }

            sb.Replace(",", $"{downloadExceptions.Count} errors were encountered while downloading:", 0, 1);
            _progress.Report(new ProgressReport(ReportType.Log, sb.ToString()));
        }

        private async Task VerifyDownloadedParts(IEnumerable<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            var failedParts = new List<M3U8.Stream>();
            var partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            var doneCount = 0;

            foreach (var part in playlist.Take(videoListCrop))
            {
                var filePath = Path.Combine(downloadFolder, RemoveQueryString(part.Path));
                if (!VerifyVideoPart(filePath))
                {
                    failedParts.Add(part);
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Verifying Parts {percent}% [3/5]"));
                _progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failedParts.Count != 0)
            {
                if (failedParts.Count == partCount)
                {
                    // Every video part returned corrupted, probably a false positive.
                    return;
                }

                _progress.Report(new ProgressReport(ReportType.Log, $"The following parts will be redownloaded: {string.Join(", ", failedParts)}"));
                await DownloadVideoPartsAsync(failedParts, videoListCrop, baseUrl, downloadFolder, vodAge, cancellationToken);
            }
        }

        private static bool VerifyVideoPart(string filePath)
        {
            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf


            if (!File.Exists(filePath))
            {
                return false;
            }

            using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileLength = fs.Length;
            if (fileLength == 0 || fileLength % TS_PACKET_LENGTH != 0)
            {
                return false;
            }

            return true;
        }

        public int RunFfmpegVideoCopy(string downloadFolder, string metadataPath, double startOffset, double seekDuration)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = downloadOptions.FfmpegPath,
                    Arguments = string.Format(
                        "-hide_banner -stats -y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {2} " : "") + "-i \"{0}\" -i \"{1}\" -map_metadata 1 -analyzeduration {3} -probesize {3} " + (downloadOptions.CropEnding ? "-t {4} " : "") + "-c:v copy \"{5}\"",
                        Path.Combine(downloadFolder, "output.ts"), metadataPath, startOffset.ToString(CultureInfo.InvariantCulture), int.MaxValue, seekDuration.ToString(CultureInfo.InvariantCulture), Path.GetFullPath(downloadOptions.Filename)),
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

                HandleFfmpegOutput(e.Data, encodingTimeRegex, seekDuration, _progress);
            };

            process.Start();
            process.BeginErrorReadLine();

            using var logWriter = File.AppendText(Path.Combine(downloadFolder, "ffmpegLog.txt"));
            do // We cannot handle logging inside the ErrorDataReceived lambda because more than 1 can come in at once and cause a race condition. lay295#598
            {
                Thread.Sleep(330);
                while (!logQueue.IsEmpty && logQueue.TryDequeue(out var logMessage))
                {
                    logWriter.WriteLine(logMessage);
                }
            } while (!process.HasExited || !logQueue.IsEmpty);

            return process.ExitCode;
        }

        private static void HandleFfmpegOutput(string output, Regex encodingTimeRegex, double videoLength, IProgress<ProgressReport> progress)
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

            var percent = (int)Math.Round(encodingTime.TotalSeconds / videoLength * 100);

            // Apparently it is possible for the percent to not be within the range of 0-100. lay295#716
            if (percent is < 0 or > 100)
            {
                progress.Report(new ProgressReport(ReportType.SameLineStatus, "Finalizing Video... [5/5]"));
                progress.Report(new ProgressReport(0));
            }
            else
            {
                progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Finalizing Video {percent}% [5/5]"));
                progress.Report(new ProgressReport(percent));
            }
        }

        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private static async Task DownloadVideoPartAsync(HttpClient httpClient, Uri baseUrl, string videoPartName, string downloadFolder, double vodAge, int throttleKib, CancellationTokenSource cancellationTokenSource)
        {
            bool tryUnmute = vodAge < 24;
            int errorCount = 0;
            int timeoutCount = 0;
            while (true)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                try
                {
                    if (tryUnmute && videoPartName.Contains("-muted"))
                    {
                        await DownloadFileAsync(httpClient, new Uri(baseUrl, videoPartName.Replace("-muted", "")), Path.Combine(downloadFolder, RemoveQueryString(videoPartName)), throttleKib, cancellationTokenSource);
                    }
                    else
                    {
                        await DownloadFileAsync(httpClient, new Uri(baseUrl, videoPartName), Path.Combine(downloadFolder, RemoveQueryString(videoPartName)), throttleKib, cancellationTokenSource);
                    }

                    return;
                }
                catch (HttpRequestException ex) when (tryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
                {
                    tryUnmute = false;
                }
                catch (HttpRequestException)
                {
                    const int maxRetries = 10;
                    if (++errorCount > maxRetries)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} failed after {maxRetries} retries");
                    }

                    await Task.Delay(1_000 * errorCount, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
                {
                    const int maxRetries = 3;
                    if (++timeoutCount > maxRetries)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} timed out {maxRetries} times");
                    }

                    await Task.Delay(5_000 * timeoutCount, cancellationTokenSource.Token);
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

            var videoListCrop = GetStreamListCrop(playlist.Streams, downloadOptions);

            return (playlist, videoListCrop, vodAge);
        }

        private static Range GetStreamListCrop(IList<M3U8.Stream> streamList, VideoDownloadOptions downloadOptions)
        {
            var startCrop = TimeSpan.FromSeconds(downloadOptions.CropBeginningTime);
            var endCrop = TimeSpan.FromSeconds(downloadOptions.CropEndingTime);

            var startIndex = 0;
            if (downloadOptions.CropBeginning)
            {
                var startTime = 0m;
                var cropTotalSeconds = (decimal)startCrop.TotalSeconds;
                foreach (var videoPart in streamList)
                {
                    if (startTime + videoPart.PartInfo.Duration > cropTotalSeconds)
                        break;

                    startIndex++;
                    startTime += videoPart.PartInfo.Duration;
                }
            }

            var endIndex = streamList.Count;
            if (downloadOptions.CropEnding)
            {
                var endTime = streamList.Sum(x => x.PartInfo.Duration);
                var cropTotalSeconds = (decimal)endCrop.TotalSeconds;
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

        /// <summary>
        /// Downloads the requested <paramref name="url"/> to the <paramref name="destinationFile"/> without storing it in memory.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> to perform the download operation.</param>
        /// <param name="url">The url of the file to download.</param>
        /// <param name="destinationFile">The path to the file where download will be saved.</param>
        /// <param name="throttleKib">The maximum download speed in kibibytes per second, or -1 for no maximum.</param>
        /// <param name="cancellationTokenSource">A <see cref="CancellationTokenSource"/> containing a <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private static async Task DownloadFileAsync(HttpClient httpClient, Uri url, string destinationFile, int throttleKib, CancellationTokenSource cancellationTokenSource = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var cancellationToken = cancellationTokenSource?.Token ?? CancellationToken.None;

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Why are we setting a CTS CancelAfter timer? See lay295#265
            const int sixtySeconds = 60;
            if (throttleKib == -1 || !response.Content.Headers.ContentLength.HasValue)
            {
                cancellationTokenSource?.CancelAfter(TimeSpan.FromSeconds(sixtySeconds));
            }
            else
            {
                const double oneKibibyte = 1024d;
                cancellationTokenSource?.CancelAfter(TimeSpan.FromSeconds(Math.Max(
                    sixtySeconds,
                    response.Content.Headers.ContentLength!.Value / oneKibibyte / throttleKib * 8 // Allow up to 8x the shortest download time given the thread bandwidth
                    )));
            }

            switch (throttleKib)
            {
                case -1:
                {
                    await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                    await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                    break;
                }
                default:
                {
                    try
                    {
                        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        await using var throttledStream = new ThrottledStream(contentStream, throttleKib);
                        await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                        await throttledStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException e) when (e.Message.Contains("EOF"))
                    {
                        // If we get an exception for EOF, it may be related to the throttler. Try again without it.
                        // TODO: Log this somehow
                        await Task.Delay(2_000, cancellationToken);
                        goto case -1;
                    }
                    break;
                }
            }

            // Reset the cts timer so it can be reused for the next download on this thread.
            // Is there a friendlier way to do this? Yes. Does it involve creating and destroying 4,000 CancellationTokenSources that are almost never cancelled? Also Yes.
            cancellationTokenSource?.CancelAfter(TimeSpan.FromMilliseconds(uint.MaxValue - 1));
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

                string partFile = Path.Combine(downloadFolder, RemoveQueryString(part.Path));
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
                _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Combining Parts {percent}% [4/5]"));
                _progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        //Some old twitch VODs have files with a query string at the end such as 1.ts?offset=blah which isn't a valid filename
        private static string RemoveQueryString(string inputString)
        {
            var queryIndex = inputString.IndexOf('?');
            if (queryIndex == -1)
            {
                return inputString;
            }

            return inputString[..queryIndex];
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