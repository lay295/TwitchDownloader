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

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetVideoChapters(downloadOptions.Id);

                string playlistUrl = await GetPlaylistUrl();
                string baseUrl = playlistUrl.Substring(0, playlistUrl.LastIndexOf('/') + 1);

                List<KeyValuePair<string, double>> videoList = new List<KeyValuePair<string, double>>();
                (List<string> videoPartsList, double vodAge) = await GetVideoPartsList(playlistUrl, videoList, cancellationToken);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/5]"));

                await DownloadVideoPartsAsync(videoPartsList, baseUrl, downloadFolder, vodAge, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Verifying Parts 0% [3/5]" });

                await VerifyDownloadedParts(videoPartsList, baseUrl, downloadFolder, vodAge, cancellationToken);

                _progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [4/5]" });

                await CombineVideoParts(downloadFolder, videoPartsList, cancellationToken);

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
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfoResponse.data.video.owner.displayName, startOffset, downloadOptions.Id,
                    videoInfoResponse.data.video.title, videoInfoResponse.data.video.createdAt, videoChapterResponse.data.video.moments.edges, cancellationToken);

                var finalizedFileDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
                if (!finalizedFileDirectory.Exists)
                {
                    TwitchHelper.CreateDirectory(finalizedFileDirectory.FullName);
                }

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await Task.Run(() => RunFfmpegVideoCopy(downloadFolder, metadataPath, seekTime, startOffset, seekDuration), cancellationToken);
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

        private async Task DownloadVideoPartsAsync(List<string> videoPartsList, string baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            var partCount = videoPartsList.Count;
            var videoPartsQueue = new ConcurrentQueue<string>(videoPartsList);
            var downloadTasks = new Task[downloadOptions.DownloadThreads];

            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadTasks[i] = StartNewDownloadThread(videoPartsQueue, baseUrl, downloadFolder, vodAge, cancellationToken);
            }

            var downloadExceptions = await WaitForDownloadThreads(downloadTasks, videoPartsQueue, baseUrl, downloadFolder, vodAge, partCount, cancellationToken);

            LogDownloadThreadExceptions(downloadExceptions);
        }

        private Task StartNewDownloadThread(ConcurrentQueue<string> videoPartsQueue, string baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(state =>
                {
                    var (partQueue, rootUrl, cacheFolder, videoAge, throttleKib, cancelToken) =
                        (Tuple<ConcurrentQueue<string>, string, string, double, int, CancellationToken>)state;

                    while (!partQueue.IsEmpty)
                    {
                        if (partQueue.TryDequeue(out var request))
                        {
                            DownloadVideoPartAsync(rootUrl, request, cacheFolder, videoAge, throttleKib, cancelToken).GetAwaiter().GetResult();
                        }

                        Task.Delay(77, cancelToken).GetAwaiter().GetResult();
                    }
                }, new Tuple<ConcurrentQueue<string>, string, string, double, int, CancellationToken>(
                    videoPartsQueue, baseUrl, downloadFolder, vodAge, downloadOptions.ThrottleKib, cancellationToken),
                cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private async Task<Dictionary<int, Exception>> WaitForDownloadThreads(Task[] tasks, ConcurrentQueue<string> videoPartsQueue, string baseUrl, string downloadFolder, double vodAge, int partCount, CancellationToken cancellationToken)
        {
            var allThreadsExited = false;
            var previousDoneCount = 0;
            var restartedThreads = 0;
            var maxRestartedThreads = Math.Max(downloadOptions.DownloadThreads, 10);
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

            return downloadExceptions;
        }

        private void LogDownloadThreadExceptions(Dictionary<int, Exception> downloadExceptions)
        {
            if (downloadExceptions.Count == 0)
                return;

            var culpritList = new List<string>();
            var sb = new StringBuilder();
            foreach (var downloadException in downloadExceptions.Values)
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

        private async Task VerifyDownloadedParts(List<string> videoParts, string baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            var failedParts = new List<string>();
            var partCount = videoParts.Count;
            var doneCount = 0;

            foreach (var part in videoParts)
            {
                if (!VerifyVideoPart(downloadFolder, part))
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
                if (failedParts.Count == videoParts.Count)
                {
                    // Every video part returned corrupted, probably a false positive.
                    return;
                }

                _progress.Report(new ProgressReport(ReportType.Log, $"The following parts will be redownloaded: {string.Join(", ", failedParts)}"));
                await DownloadVideoPartsAsync(failedParts, baseUrl, downloadFolder, vodAge, cancellationToken);
            }
        }

        private static bool VerifyVideoPart(string downloadFolder, string part)
        {
            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf

            var partFile = Path.Combine(downloadFolder, RemoveQueryString(part));
            if (!File.Exists(partFile))
            {
                return false;
            }

            using var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.None);
            var fileLength = fs.Length;
            if (fileLength == 0 || fileLength % TS_PACKET_LENGTH != 0)
            {
                return false;
            }

            return true;
        }

        private int RunFfmpegVideoCopy(string downloadFolder, string metadataPath, double seekTime, double startOffset, double seekDuration)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = downloadOptions.FfmpegPath,
                    Arguments = string.Format(
                        "-hide_banner -stats -y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {2} " : "") + "-i \"{0}\" -i \"{1}\" -map_metadata 1 -analyzeduration {3} -probesize {3} " + (downloadOptions.CropEnding ? "-t {4} " : "") + "-c:v copy \"{5}\"",
                        Path.Combine(downloadFolder, "output.ts"), metadataPath, (seekTime - startOffset).ToString(CultureInfo.InvariantCulture), int.MaxValue, seekDuration.ToString(CultureInfo.InvariantCulture), Path.GetFullPath(downloadOptions.Filename)),
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
                progress.Report(new ProgressReport(ReportType.SameLineStatus, "Finalizing Video... [4/4]"));
                progress.Report(new ProgressReport(0));
            }
            else
            {
                progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Finalizing Video {percent}% [4/4]"));
                progress.Report(new ProgressReport(percent));
            }
        }

        private async Task DownloadVideoPartAsync(string baseUrl, string videoPartName, string downloadFolder, double vodAge, int throttleKib, CancellationToken cancellationToken)
        {
            bool tryUnmute = vodAge < 24;
            int errorCount = 0;
            int timeoutCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (tryUnmute && videoPartName.Contains("-muted"))
                    {
                        await DownloadFileTaskAsync(baseUrl + videoPartName.Replace("-muted", ""), Path.Combine(downloadFolder, RemoveQueryString(videoPartName)), throttleKib, cancellationToken);
                    }
                    else
                    {
                        await DownloadFileTaskAsync(baseUrl + videoPartName, Path.Combine(downloadFolder, RemoveQueryString(videoPartName)), throttleKib, cancellationToken);
                    }

                    return;
                }
                catch (HttpRequestException ex) when (tryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
                {
                    tryUnmute = false;
                }
                catch (HttpRequestException)
                {
                    if (++errorCount > 10)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} failed after 10 retries");
                    }

                    await Task.Delay(1_000 * errorCount, cancellationToken);
                }
                catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
                {
                    if (++timeoutCount > 3)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} timed out 3 times");
                    }

                    await Task.Delay(5_000 * timeoutCount, cancellationToken);
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
                            pair = new KeyValuePair<string, double>(pair.Key, pair.Value + Double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 2], Double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture)));
                        }
                    }
                    else
                    {
                        videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 1], Double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture)));
                    }
                }
            }

            List<KeyValuePair<string, double>> videoListCropped = GenerateCroppedVideoList(videoList, downloadOptions);

            List<string> videoParts = new List<string>(videoListCropped.Count);
            foreach (var part in videoListCropped)
            {
                videoParts.Add(part.Key);
            }

            return (videoParts, vodAge);
        }

        private async Task<string> GetPlaylistUrl()
        {
            GqlVideoTokenResponse accessToken = await TwitchHelper.GetVideoToken(downloadOptions.Id, downloadOptions.Oauth);

            if (accessToken.data.videoPlaybackAccessToken is null)
            {
                throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
            }

            string[] videoPlaylist = await TwitchHelper.GetVideoPlaylist(downloadOptions.Id, accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (videoPlaylist[0].Contains("vod_manifest_restricted"))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            List<KeyValuePair<string, string>> videoQualities = new List<KeyValuePair<string, string>>();

            for (int i = 0; i < videoPlaylist.Length; i++)
            {
                if (videoPlaylist[i].Contains("#EXT-X-MEDIA"))
                {
                    string lastPart = videoPlaylist[i].Substring(videoPlaylist[i].IndexOf("NAME=\"") + 6);
                    string stringQuality = lastPart.Substring(0, lastPart.IndexOf("\""));

                    if (!videoQualities.Any(x => x.Key.Equals(stringQuality)))
                    {
                        videoQualities.Add(new KeyValuePair<string, string>(stringQuality, videoPlaylist[i + 2]));
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

        /// <summary>
        /// Downloads the requested <paramref name="url"/> to the <paramref name="destinationFile"/> without storing it in memory.
        /// </summary>
        /// <param name="url">The url of the file to download.</param>
        /// <param name="destinationFile">The path to the file where download will be saved.</param>
        /// <param name="throttleKib">The maximum download speed in kibibytes per second, or -1 for no maximum.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        private async Task DownloadFileTaskAsync(string url, string destinationFile, int throttleKib, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

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
                        await using var throttledStream = new ThrottledStream(await response.Content.ReadAsStreamAsync(cancellationToken), throttleKib);
                        await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                        await throttledStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException e) when (e.Message.Contains("EOF"))
                    {
                        // The throttled stream throws when it reads an unexpected EOF, try again without the limiter
                        // TODO: Log this somehow
                        await Task.Delay(2_000, cancellationToken);
                        goto case -1;
                    }
                    break;
                }
            }
        }

        private async Task CombineVideoParts(string downloadFolder, List<string> videoParts, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(downloadFolder);
            string outputFile = Path.Combine(downloadFolder, "output.ts");

            int partCount = videoParts.Count;
            int doneCount = 0;

            await using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            foreach (var part in videoParts)
            {
                await DriveHelper.WaitForDrive(outputDrive, _progress, cancellationToken);

                string partFile = Path.Combine(downloadFolder, RemoveQueryString(part));
                if (File.Exists(partFile))
                {
                    await using (var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.None))
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

        private static List<KeyValuePair<string, double>> GenerateCroppedVideoList(List<KeyValuePair<string, double>> videoList, VideoDownloadOptions downloadOptions)
        {
            List<KeyValuePair<string, double>> returnList = new List<KeyValuePair<string, double>>(videoList);
            TimeSpan startCrop = TimeSpan.FromSeconds(downloadOptions.CropBeginningTime);
            TimeSpan endCrop = TimeSpan.FromSeconds(downloadOptions.CropEndingTime);

            if (downloadOptions.CropBeginning)
            {
                double startTime = 0;
                for (int i = 0; i < returnList.Count; i++)
                {
                    if (startTime + returnList[i].Value < startCrop.TotalSeconds)
                    {
                        startTime += returnList[i].Value;
                        returnList.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (downloadOptions.CropEnding)
            {
                double endTime = 0.0;
                videoList.ForEach(x => endTime += x.Value);

                for (int i = returnList.Count - 1; i >= 0; i--)
                {
                    if (endTime - returnList[i].Value > endCrop.TotalSeconds)
                    {
                        endTime -= returnList[i].Value;
                        returnList.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return returnList;
        }
    }
}