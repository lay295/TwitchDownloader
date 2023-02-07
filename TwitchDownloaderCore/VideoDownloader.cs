using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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

        public VideoDownloader(VideoDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
        }

        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            TwitchHelper.CleanupUnmanagedCacheFiles(downloadOptions.TempFolder);

            string downloadFolder = Path.Combine(
                downloadOptions.TempFolder,
                $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

            progress.Report(new ProgressReport(ReportType.SameLineStatus, "Fetching Video Info [1/4]"));

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
                string baseUrl = playlistUrl.Substring(0, playlistUrl.LastIndexOf("/") + 1);

                List<KeyValuePair<string, double>> videoList = new List<KeyValuePair<string, double>>();
                (List<string> videoPartsList, double vodAge) = await GetVideoPartsList(playlistUrl, videoList, cancellationToken);
                int partCount = videoPartsList.Count;
                int doneCount = 0;

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading 0% [2/4]"));

                using (var throttler = new SemaphoreSlim(downloadOptions.DownloadThreads))
                {
                    Task[] downloadTasks = videoPartsList.Select(request => Task.Run(async () =>
                    {
                        await throttler.WaitAsync();
                        try
                        {
                            await DownloadVideoPart(baseUrl, request, downloadFolder, vodAge, cancellationToken);

                            doneCount++;
                            int percent = (int)(doneCount / (double)partCount * 100);
                            progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Downloading {percent}% [2/4]"));
                            progress.Report(new ProgressReport(percent));
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    })).ToArray();
                    await Task.WhenAll(downloadTasks);
                }

                progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Combining Parts 0% [3/4]" });

                await CombineVideoParts(downloadFolder, videoPartsList, progress, cancellationToken);

                progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = $"Finalizing Video 0% [4/4]" });

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
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfoResponse.data.video.owner.displayName, startOffset, downloadOptions.Id, videoInfoResponse.data.video.title, videoInfoResponse.data.video.createdAt, videoChapterResponse.data.video.moments.edges, cancellationToken);

                await Task.Run(() =>
                {
                    var process = new Process
                    {
                        StartInfo =
                            {
                                FileName = downloadOptions.FfmpegPath,
                                Arguments = String.Format("-hide_banner -stats -y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {2} " : "") + "-i \"{0}\" -i {1} -map_metadata 1 -analyzeduration {3} -probesize {3} " + (downloadOptions.CropEnding ? "-t {4} " : "") + "-c:v copy \"{5}\"", Path.Combine(downloadFolder, "output.ts"), metadataPath, (seekTime - startOffset).ToString(CultureInfo.InvariantCulture), Int32.MaxValue, seekDuration.ToString(CultureInfo.InvariantCulture), Path.GetFullPath(downloadOptions.Filename)),
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardInput = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            }
                    };

                    TimeSpan videoLength = TimeSpan.FromTicks(0);
                    var videoLengthRegex = new Regex(@"^\s?\s?Duration: (\d\d:\d\d:\d\d.\d\d)", RegexOptions.Multiline);
                    var encodingTimeRegex = new Regex(@"time=(\d\d:\d\d:\d\d.\d\d)", RegexOptions.Compiled);
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data is null)
                        {
                            return;
                        }

                        if (videoLength.Ticks == 0)
                        {
                            var videoLengthMatch = videoLengthRegex.Match(e.Data);
                            if (!videoLengthMatch.Success)
                            {
                                return;
                            }
                            videoLength = TimeSpan.Parse(videoLengthMatch.Groups[1].ToString());
                        }

                        HandleFfmpegProgress(e.Data, encodingTimeRegex, videoLength, progress);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    progress.Report(new ProgressReport(0));
                }, cancellationToken);
            }
            finally
            {
                Cleanup(downloadFolder);
            }
        }

        private static void HandleFfmpegProgress(string output, Regex encodingTimeRegex, TimeSpan videoLength, IProgress<ProgressReport> progress)
        {
            if (videoLength.Ticks == 0)
            {
                return;
            }

            var encodingTimeMatch = encodingTimeRegex.Match(output);
            if (!encodingTimeMatch.Success)
            {
                return;
            }

            var encodingTime = TimeSpan.Parse(encodingTimeMatch.Groups[1].ToString());
            int percent = (int)(encodingTime.TotalMilliseconds / videoLength.TotalMilliseconds * 100);

            progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Finalizing Video {percent}% [4/4]"));
            progress.Report(new ProgressReport(percent));
        }

        private async Task DownloadVideoPart(string baseUrl, string videoPartName, string downloadFolder, double vodAge, CancellationToken cancellationToken)
        {
            bool tryUnmute = vodAge < 24;
            int errorCount = 0;
            int timeoutCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // There is a cleaner way to do delayed retries with Polly but in this
                // scenario we need more control than just blindly retrying
                try
                {
                    if (tryUnmute && videoPartName.Contains("-muted"))
                    {
                        await DownloadFileTaskAsync(baseUrl + videoPartName.Replace("-muted", ""), Path.Combine(downloadFolder, RemoveQueryString(videoPartName)), cancellationToken);
                    }
                    else
                    {
                        await DownloadFileTaskAsync(baseUrl + videoPartName, Path.Combine(downloadFolder, RemoveQueryString(videoPartName)), cancellationToken);
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
                return videoQualities.Where(x => x.Key.StartsWith(downloadOptions.Quality)).First().Value;
            }

            // Unable to find specified quality, defaulting to highest quality
            return videoQualities.First().Value;
        }

        /// <summary>
        /// Downloads the requested <paramref name="Url"/> to the <paramref name="destinationFile"/> without storing it in memory
        /// </summary>
        private async Task DownloadFileTaskAsync(string Url, string destinationFile, CancellationToken cancellationToken = new())
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Url);

            // We must specify HttpCompletionOption.ResponseHeadersRead or it will read the response content into memory
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task CombineVideoParts(string downloadFolder, List<string> videoParts, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(downloadFolder);
            string outputFile = Path.Combine(downloadFolder, "output.ts");

            int partCount = videoParts.Count;
            int doneCount = 0;

            using FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            foreach (var part in videoParts)
            {
                await DriveHelper.WaitForDrive(outputDrive, progress, cancellationToken);

                string partFile = Path.Combine(downloadFolder, RemoveQueryString(part));
                if (File.Exists(partFile))
                {
                    using (var fs = File.Open(partFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        await fs.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                    }

                    try
                    {
                        File.Delete(partFile);
                    }
                    catch { }
                }

                doneCount++;
                int percent = (int)(doneCount / (double)partCount * 100);
                progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Combining Parts {percent}% [3/4]"));
                progress.Report(new ProgressReport(percent));

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        //Some old twitch VODs have files with a query string at the end such as 1.ts?offset=blah which isn't a valid filename
        private static string RemoveQueryString(string inputString)
        {
            if (inputString.Contains('?'))
            {
                return inputString.Split('?')[0];
            }
            else
            {
                return inputString;
            }
        }

        private void Cleanup(string downloadFolder)
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
