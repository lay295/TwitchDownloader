using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        private static HttpClient httpClient = new HttpClient();

        public VideoDownloader(VideoDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
        }

        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            string downloadFolder = Path.Combine(
                downloadOptions.TempFolder,
                $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

            progress.Report(new ProgressReport(ReportType.Status, "Fetching Video Info [1/4]"));

            try
            {
                ServicePointManager.DefaultConnectionLimit = downloadOptions.DownloadThreads;

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                string playlistUrl;

                if (downloadOptions.PlaylistUrl == null)
                {
                    Task<GqlVideoTokenResponse> taskAccessToken = TwitchHelper.GetVideoToken(downloadOptions.Id, downloadOptions.Oauth);
                    await taskAccessToken;

                    string[] videoPlaylist = await TwitchHelper.GetVideoPlaylist(downloadOptions.Id, taskAccessToken.Result.data.videoPlaybackAccessToken.value, taskAccessToken.Result.data.videoPlaybackAccessToken.signature);
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
                        playlistUrl = videoQualities.Where(x => x.Key.StartsWith(downloadOptions.Quality)).First().Value;
                    else
                    {
                        //Unable to find specified quality, defaulting to highest quality
                        playlistUrl = videoQualities.First().Value;
                    }
                }
                else
                {
                    playlistUrl = downloadOptions.PlaylistUrl;
                }

                string baseUrl = playlistUrl.Substring(0, playlistUrl.LastIndexOf("/") + 1);
                List<KeyValuePair<string, double>> videoList = new List<KeyValuePair<string, double>>();

                double vodAge = 25;

                string[] videoChunks = (await httpClient.GetStringAsync(playlistUrl)).Split('\n');

                try
                {
                    vodAge = (DateTimeOffset.UtcNow - DateTimeOffset.Parse(videoChunks.First(x => x.Contains("#ID3-EQUIV-TDTG:")).Replace("#ID3-EQUIV-TDTG:", ""))).TotalHours;
                }
                catch { }

                for (int i = 0; i < videoChunks.Length; i++)
                {
                    if (videoChunks[i].Contains("#EXTINF"))
                    {
                        if (videoChunks[i + 1].Contains("#EXT-X-BYTERANGE"))
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
                Queue<string> videoParts = new Queue<string>();
                videoListCropped.ForEach(x => videoParts.Enqueue(x.Key));
                List<string> videoPartsList = new List<string>(videoParts);
                int partCount = videoParts.Count;
                int doneCount = 0;

                progress.Report(new ProgressReport(ReportType.StatusInfo, "Downloading 0% [2/4]"));

                using (var throttler = new SemaphoreSlim(downloadOptions.DownloadThreads))
                {
                    Task[] downloadTasks = videoParts.Select(request => Task.Run(async () =>
                    {
                        await throttler.WaitAsync();
                        try
                        {
                            bool isDone = false;
                            bool tryUnmute = vodAge < 24;
                            int errorCount = 0;
                            while (!isDone)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                // There is a builtin way to do delayed retries with HttpClient but in this
                                // specific case we need more control than just blindly retrying
                                try
                                {
                                    if (tryUnmute && request.Contains("-muted"))
                                    {
                                        await DownloadFileTaskAsync(baseUrl + request.Replace("-muted", ""), Path.Combine(downloadFolder, RemoveQueryString(request)), cancellationToken);
                                    }
                                    else
                                    {
                                        await DownloadFileTaskAsync(baseUrl + request, Path.Combine(downloadFolder, RemoveQueryString(request)), cancellationToken);
                                    }

                                    isDone = true;
                                }
                                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden && tryUnmute)
                                {
                                    tryUnmute = false;
                                }
                                catch (HttpRequestException)
                                {
                                    if (++errorCount > 10)
                                    {
                                        throw new HttpRequestException("Video part " + request + " failed after 10 retries");
                                    }

                                    await Task.Delay(10_000);
                                }
                            }

                            doneCount++;
                            int percent = (int)(doneCount / (double)partCount * 100);
                            progress.Report(new ProgressReport() { ReportType = ReportType.StatusInfo, Data = string.Format("Downloading {0}% [2/4]", percent) });
                            progress.Report(new ProgressReport() { ReportType = ReportType.Percent, Data = percent });

                            return;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException or TaskCanceledException)
                        {
                            Debug.WriteLine(ex);
                        }
                        finally
                        {
                            throttler.Release();
                            CheckCancelation(cancellationToken, downloadFolder);
                        }
                    })).ToArray();
                    await Task.WhenAll(downloadTasks);
                }

                CheckCancelation(cancellationToken, downloadFolder);

                progress.Report(new ProgressReport() { ReportType = ReportType.Status, Data = "Combining Parts [3/4]" });
                progress.Report(new ProgressReport() { ReportType = ReportType.Percent, Data = 0 });

                await CombineVideoParts(progress, downloadFolder, videoPartsList, cancellationToken);

                progress.Report(new ProgressReport() { ReportType = ReportType.Status, Data = $"Finalizing Video [4/4]" });

                double startOffset = 0.0;

                for (int i = 0; i < videoList.Count; i++)
                {
                    if (videoList[i].Key == videoPartsList[0])
                        break;

                    startOffset += videoList[i].Value;
                }

                double seekTime = downloadOptions.CropBeginningTime;
                double seekDuration = Math.Round(downloadOptions.CropEndingTime - seekTime);

                await Task.Run(() =>
                {
                    var process = new Process
                    {
                        StartInfo =
                            {
                                FileName = downloadOptions.FfmpegPath,
                                Arguments = String.Format("-hide_banner -loglevel error -stats -y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {1} " : "") + "-i \"{0}\" -analyzeduration {2} -probesize {2} " + (downloadOptions.CropEnding ? "-t {3} " : "") + "-c:v copy \"{4}\"", Path.Combine(downloadFolder, "output.ts"), (seekTime - startOffset).ToString(CultureInfo.InvariantCulture), Int32.MaxValue, seekDuration.ToString(CultureInfo.InvariantCulture), Path.GetFullPath(downloadOptions.Filename)),
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardInput = false,
                                RedirectStandardOutput = false,
                                RedirectStandardError = false
                            }
                    };
                    process.Start();
                    process.WaitForExit();
                }, cancellationToken);
                Cleanup(downloadFolder);
            }
            catch
            {
                Cleanup(downloadFolder);
                throw;
            }
        }

        /// <summary>
        /// Downloads the requested <paramref name="Url"/> to the <paramref name="destinationFile"/> without storing it in memory
        /// </summary>
        private static async Task DownloadFileTaskAsync(string Url, string destinationFile, CancellationToken cancellationToken = new())
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Url);

            // We must specify HttpCompletionOption.ResponseHeadersRead or it will read the response content into memory
            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                using (var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task CombineVideoParts(IProgress<ProgressReport> progress, string downloadFolder, List<string> videoPartsList, CancellationToken cancellationToken)
        {
            DriveInfo outputDrive = DriveHelper.GetOutputDrive(downloadFolder);

            string outputFile = Path.Combine(downloadFolder, "output.ts");
            using (FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (var part in videoPartsList)
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
                    CheckCancelation(cancellationToken, downloadFolder);
                }
            }
        }

        //Some old twitch VODs have files with a query string at the end such as 1.ts?offset=blah which isn't a valid filename
        private string RemoveQueryString(string inputString)
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
            if (Directory.Exists(downloadFolder))
                Directory.Delete(downloadFolder, true);
        }

        private void CheckCancelation(CancellationToken cancellationToken, string downloadFolder)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Cleanup(downloadFolder);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private List<KeyValuePair<string, double>> GenerateCroppedVideoList(List<KeyValuePair<string, double>> videoList, VideoDownloadOptions downloadOptions)
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
