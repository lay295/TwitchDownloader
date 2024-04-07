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
            downloadOptions.CropBeginningTime = downloadOptions.CropBeginningTime >= TimeSpan.Zero ? downloadOptions.CropBeginningTime : TimeSpan.Zero;
            downloadOptions.CropEndingTime = downloadOptions.CropEndingTime >= TimeSpan.Zero ? downloadOptions.CropEndingTime : TimeSpan.Zero;
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

                startOffset = downloadOptions.CropBeginningTime - startOffset;
                var seekDuration = downloadOptions.CropEndingTime - downloadOptions.CropBeginningTime;

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                VideoInfo videoInfo = videoInfoResponse.data.video;
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.owner.displayName, downloadOptions.Id.ToString(), videoInfo.title, videoInfo.createdAt, videoInfo.viewCount,
                    videoInfo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd(), downloadOptions.CropBeginningTime,
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
                downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : TimeSpan.Zero,
                downloadOptions.CropEnding ? downloadOptions.CropEndingTime : videoLength);
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
            var downloadThreads = new DownloadThread[downloadOptions.DownloadThreads];

            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadThreads[i] = new DownloadThread(videoPartsQueue, _httpClient, baseUrl, downloadFolder, vodAirDate, downloadOptions.ThrottleKib, _progress, cancellationToken);
            }

            var downloadExceptions = await WaitForDownloadThreads(downloadThreads, videoPartsQueue, partCount, cancellationToken);

            LogDownloadThreadExceptions(downloadExceptions);
        }

        private async Task<IReadOnlyCollection<Exception>> WaitForDownloadThreads(DownloadThread[] downloadThreads, ConcurrentQueue<string> videoPartsQueue, int partCount, CancellationToken cancellationToken)
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
                            thread.StartDownloadThread();
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
            _progress.LogInfo(sb.ToString());
        }

        private async Task VerifyDownloadedParts(ICollection<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, DateTimeOffset vodAirDate, CancellationToken cancellationToken)
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
                _progress.ReportProgress(percent);

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failedParts.Count != 0)
            {
                if (playlist.Count == 1)
                {
                    // The video is only 1 part, it probably won't be a complete file.
                    return;
                }

                if (partCount > 20 && failedParts.Count >= partCount * 0.95)
                {
                    // 19/20 parts failed to verify. Either the VOD is heavily corrupted or something went horribly wrong.
                    // TODO: Somehow let the user bypass this. Maybe with callbacks?
                    throw new Exception($"Too many parts are corrupted or missing ({failedParts}/{partCount}), aborting.");
                }

                _progress.LogInfo($"The following parts will be redownloaded: {string.Join(", ", failedParts)}");
                await DownloadVideoPartsAsync(failedParts, videoListCrop, baseUrl, downloadFolder, vodAirDate, cancellationToken);
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

        private int RunFfmpegVideoCopy(string downloadFolder, string metadataPath, TimeSpan startOffset, TimeSpan seekDuration)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = downloadOptions.FfmpegPath,
                    Arguments = string.Format(
                        "-hide_banner -stats -y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {2} " : "") + "-i \"{0}\" -i \"{1}\" -map_metadata 1 -analyzeduration {3} -probesize {3} " + (downloadOptions.CropEnding ? "-t {4} " : "") + "-c:v copy \"{5}\"",
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
            if (downloadOptions.CropBeginning)
            {
                var startTime = 0m;
                var cropTotalSeconds = (decimal)downloadOptions.CropBeginningTime.TotalSeconds;
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
                var cropTotalSeconds = (decimal)downloadOptions.CropEndingTime.TotalSeconds;
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
        /// <param name="logger">Logger.</param>
        /// <param name="cancellationTokenSource">A <see cref="CancellationTokenSource"/> containing a <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private static async Task DownloadFileAsync(HttpClient httpClient, Uri url, string destinationFile, int throttleKib, ITaskLogger logger, CancellationTokenSource cancellationTokenSource = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var cancellationToken = cancellationTokenSource?.Token ?? CancellationToken.None;

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Why are we setting a CTS CancelAfter timer? See lay295#265
            const int SIXTY_SECONDS = 60;
            if (throttleKib == -1 || !response.Content.Headers.ContentLength.HasValue)
            {
                cancellationTokenSource?.CancelAfter(TimeSpan.FromSeconds(SIXTY_SECONDS));
            }
            else
            {
                const double ONE_KIBIBYTE = 1024d;
                cancellationTokenSource?.CancelAfter(TimeSpan.FromSeconds(Math.Max(
                    SIXTY_SECONDS,
                    response.Content.Headers.ContentLength!.Value / ONE_KIBIBYTE / throttleKib * 8 // Allow up to 8x the shortest download time given the thread bandwidth
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
                    catch (IOException ex) when (ex.Message.Contains("EOF"))
                    {
                        // If we get an exception for EOF, it may be related to the throttler. Try again without it.
                        logger.LogVerbose($"Unexpected EOF, retrying without bandwidth throttle. Message: {ex.Message}.");
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
                _progress.ReportProgress(percent);

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

        // TODO: Move to separate file
        private sealed record DownloadThread
        {
            private readonly ConcurrentQueue<string> _videoPartsQueue;
            private readonly HttpClient _client;
            private readonly Uri _baseUrl;
            private readonly string _cacheFolder;
            private readonly DateTimeOffset _vodAirDate;
            private TimeSpan VodAge => DateTimeOffset.UtcNow - _vodAirDate;
            private readonly int _throttleKib;
            private readonly ITaskLogger _logger;
            private readonly CancellationToken _cancellationToken;
            public Task ThreadTask { get; private set; }

            public DownloadThread(ConcurrentQueue<string> videoPartsQueue, HttpClient httpClient, Uri baseUrl, string cacheFolder, DateTimeOffset vodAirDate, int throttleKib, ITaskLogger logger, CancellationToken cancellationToken)
            {
                _videoPartsQueue = videoPartsQueue;
                _client = httpClient;
                _baseUrl = baseUrl;
                _cacheFolder = cacheFolder;
                _vodAirDate = vodAirDate;
                _throttleKib = throttleKib;
                _logger = logger;
                _cancellationToken = cancellationToken;
                StartDownloadThread();
            }

            public void StartDownloadThread()
            {
                if (ThreadTask is { Status: TaskStatus.Created or TaskStatus.WaitingForActivation or TaskStatus.WaitingToRun or TaskStatus.Running })
                {
                    throw new InvalidOperationException($"Tried to start a thread that was already running or waiting to run ({ThreadTask.Status}).");
                }

                ThreadTask = Task.Factory.StartNew(
                    ExecuteDownloadThread,
                    _cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Current);
            }

            private void ExecuteDownloadThread()
            {
                using var cts = new CancellationTokenSource();
                _cancellationToken.Register(PropagateCancel, cts);

                while (!_videoPartsQueue.IsEmpty)
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    string videoPart = null;
                    try
                    {
                        if (_videoPartsQueue.TryDequeue(out videoPart))
                        {
                            DownloadVideoPartAsync(videoPart, cts).GetAwaiter().GetResult();
                        }
                    }
                    catch
                    {
                        if (videoPart != null && !_cancellationToken.IsCancellationRequested)
                        {
                            // Requeue the video part now instead of deferring to the verifier since we already know it's bad
                            _videoPartsQueue.Enqueue(videoPart);
                        }

                        throw;
                    }

                    const int A_PRIME_NUMBER = 71;
                    Thread.Sleep(A_PRIME_NUMBER);
                }
            }

            private static void PropagateCancel(object tokenSourceToCancel)
            {
                try
                {
                    (tokenSourceToCancel as CancellationTokenSource)?.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
            private async Task DownloadVideoPartAsync(string videoPartName, CancellationTokenSource cancellationTokenSource)
            {
                var tryUnmute = VodAge < TimeSpan.FromHours(24);
                var errorCount = 0;
                var timeoutCount = 0;
                while (true)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    try
                    {
                        var partFile = Path.Combine(_cacheFolder, RemoveQueryString(videoPartName));
                        if (tryUnmute && videoPartName.Contains("-muted"))
                        {
                            var unmutedPartName = videoPartName.Replace("-muted", "");
                            await DownloadFileAsync(_client, new Uri(_baseUrl, unmutedPartName), partFile, _throttleKib, _logger, cancellationTokenSource);
                        }
                        else
                        {
                            await DownloadFileAsync(_client, new Uri(_baseUrl, videoPartName), partFile, _throttleKib, _logger, cancellationTokenSource);
                        }

                        return;
                    }
                    catch (HttpRequestException ex) when (tryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
                    {
                        _logger.LogVerbose($"Received {ex.StatusCode}: {ex.StatusCode} when trying to unmute {videoPartName}. Disabling {nameof(tryUnmute)}.");
                        tryUnmute = false;

                        await Task.Delay(100, cancellationTokenSource.Token);
                    }
                    catch (HttpRequestException ex)
                    {
                        const int MAX_RETRIES = 10;

                        _logger.LogVerbose($"Received {(int)(ex.StatusCode ?? 0)}: {ex.StatusCode} for {videoPartName}. {MAX_RETRIES - (errorCount + 1)} retries left.");
                        if (++errorCount > MAX_RETRIES)
                        {
                            throw new HttpRequestException($"Video part {videoPartName} failed after {MAX_RETRIES} retries");
                        }

                        await Task.Delay(1_000 * errorCount, cancellationTokenSource.Token);
                    }
                    catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
                    {
                        const int MAX_RETRIES = 3;

                        _logger.LogVerbose($"{videoPartName} timed out. {MAX_RETRIES - (timeoutCount + 1)} retries left.");
                        if (++timeoutCount > MAX_RETRIES)
                        {
                            throw new HttpRequestException($"Video part {videoPartName} timed out {MAX_RETRIES} times");
                        }

                        await Task.Delay(5_000 * timeoutCount, cancellationTokenSource.Token);
                    }
                }
            }
        }
    }
}