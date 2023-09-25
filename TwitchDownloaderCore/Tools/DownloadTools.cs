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
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tools
{
    public class DownloadTools
    {
        private static readonly HttpClient HttpClient = new();
        public static async Task DownloadClipFileTaskAsync(string url, string destinationFile, int throttleKib, IProgress<StreamCopyProgress> progress, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;

            if (throttleKib == -1)
            {
                await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await contentStream.ProgressCopyToAsync(fs, contentLength, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var throttledStream = new ThrottledStream(contentStream, throttleKib);
                await throttledStream.ProgressCopyToAsync(fs, contentLength, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        public static List<KeyValuePair<string, double>> GenerateCroppedVideoList(List<KeyValuePair<string, double>> videoList, VideoDownloadOptions downloadOptions)
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

        public static void LogDownloadThreadExceptions(Dictionary<int, Exception> downloadExceptions, IProgress<ProgressReport> progress)
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
            progress.Report(new ProgressReport(ReportType.Log, sb.ToString()));
        }

        public static Task StartNewDownloadThread(ConcurrentQueue<string> videoPartsQueue, VideoDownloadOptions downloadOptions, string baseUrl, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(state =>
            {
                var (partQueue, rootUrl, cacheFolder, videoAge, throttleKib, cancelToken) =
                    (Tuple<ConcurrentQueue<string>, string, string, double, int, CancellationToken>)state;

                while (!partQueue.IsEmpty)
                {
                    if (partQueue.TryDequeue(out var request))
                    {
                        DownloadVideoPartAsync(rootUrl, downloadOptions, request, cacheFolder, videoAge, throttleKib, cancelToken).GetAwaiter().GetResult();
                    }

                    Task.Delay(77, cancelToken).GetAwaiter().GetResult();
                }
            }, new Tuple<ConcurrentQueue<string>, string, string, double, int, CancellationToken>(
                    videoPartsQueue, baseUrl, downloadFolder, vodAge, downloadOptions.ThrottleKib, cancellationToken),
                cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        public static async Task<Dictionary<int, Exception>> WaitForDownloadThreads(Task[] tasks, VideoDownloadOptions downloadOptions, ConcurrentQueue<string> videoPartsQueue, string baseUrl, string downloadFolder, double vodAge, int partCount, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
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
                    progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Downloading {percent}% [2/5]"));
                    progress.Report(new ProgressReport(percent));
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
                            tasks[t] = StartNewDownloadThread(videoPartsQueue, downloadOptions, baseUrl, downloadFolder, vodAge, cancellationToken);
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

        private static async Task DownloadVideoPartAsync(string baseUrl, VideoDownloadOptions downloadOptions, string videoPartName, string downloadFolder, double vodAge, int throttleKib, CancellationToken cancellationToken)
        {
            bool tryUnmute = vodAge < 24 && downloadOptions.VideoPlatform == VideoPlatform.Twitch;
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

        /// <summary>
        /// Downloads the requested <paramref name="url"/> to the <paramref name="destinationFile"/> without storing it in memory.
        /// </summary>
        /// <param name="url">The url of the file to download.</param>
        /// <param name="destinationFile">The path to the file where download will be saved.</param>
        /// <param name="throttleKib">The maximum download speed in kibibytes per second, or -1 for no maximum.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        public static async Task DownloadFileTaskAsync(string url, string destinationFile, int throttleKib, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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

        public static async Task VerifyDownloadedParts(VideoDownloadOptions downloadOptions, List<string> videoParts, string baseUrl, string downloadFolder, double vodAge, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
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
                progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Verifying Parts {percent}% [3/5]"));
                progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failedParts.Count != 0)
            {
                if (failedParts.Count == videoParts.Count)
                {
                    // Every video part returned corrupted, probably a false positive.
                    return;
                }

                progress.Report(new ProgressReport(ReportType.Log, $"The following parts will be redownloaded: {string.Join(", ", failedParts)}"));
                await DownloadVideoPartsAsync(downloadOptions, failedParts, baseUrl, downloadFolder, vodAge, progress, cancellationToken);
            }
        }

        public static async Task DownloadVideoPartsAsync(VideoDownloadOptions downloadOptions, List<string> videoPartsList, string baseUrl, string downloadFolder, double vodAge, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            var partCount = videoPartsList.Count;
            var videoPartsQueue = new ConcurrentQueue<string>(videoPartsList);
            var downloadTasks = new Task[downloadOptions.DownloadThreads];

            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadTasks[i] = DownloadTools.StartNewDownloadThread(videoPartsQueue, downloadOptions, baseUrl, downloadFolder, vodAge, cancellationToken);
            }

            var downloadExceptions = await DownloadTools.WaitForDownloadThreads(downloadTasks, downloadOptions, videoPartsQueue, baseUrl, downloadFolder, vodAge, partCount, progress, cancellationToken);

            DownloadTools.LogDownloadThreadExceptions(downloadExceptions, progress);
        }

        public static async Task CombineVideoParts(string downloadFolder, List<string> videoParts, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(downloadFolder);
            string outputFile = Path.Combine(downloadFolder, "output.ts");

            int partCount = videoParts.Count;
            int doneCount = 0;

            await using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            foreach (var part in videoParts)
            {
                await DriveHelper.WaitForDrive(outputDrive, progress, cancellationToken);

                string partFile = Path.Combine(downloadFolder, DownloadTools.RemoveQueryString(part));
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
                progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Combining Parts {percent}% [4/5]"));
                progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public static int RunFfmpegVideoCopy(VideoDownloadOptions downloadOptions, string downloadFolder, string metadataPath, double seekTime, double startOffset, double seekDuration, IProgress<ProgressReport> progress)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = downloadOptions.FfmpegPath,
                    Arguments = string.Format(
                        "-hide_banner -stats -y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {2} " : "") + "-i \"{0}\" -i \"{1}\" -map_metadata 1 -analyzeduration {3} -probesize {3} " + (downloadOptions.CropEnding ? "-t {4} " : "") + "-c copy \"{5}\"",
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

                HandleFfmpegOutput(e.Data, encodingTimeRegex, seekDuration, progress);
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

        private static bool VerifyVideoPart(string downloadFolder, string part)
        {
            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf

            var partFile = Path.Combine(downloadFolder, DownloadTools.RemoveQueryString(part));
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

        //Some old twitch VODs have files with a query string at the end such as 1.ts?offset=blah which isn't a valid filename
        public static string RemoveQueryString(string inputString)
        {
            var queryIndex = inputString.IndexOf('?');
            if (queryIndex == -1)
            {
                return inputString;
            }

            return inputString[..queryIndex];
        }

        public static void Cleanup(string downloadFolder)
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
