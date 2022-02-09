using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore
{
    public class VideoDownloader
    {
        VideoDownloadOptions downloadOptions;

        public VideoDownloader(VideoDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
        }

        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            string tempFolder = downloadOptions.TempFolder == null || downloadOptions.TempFolder == "" ?  Path.Combine(Path.GetTempPath(), "TwitchDownloader") : Path.Combine(downloadOptions.TempFolder, "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, downloadOptions.Id.ToString() == "0" ? Guid.NewGuid().ToString() : downloadOptions.Id.ToString());
            try
            {
                ServicePointManager.DefaultConnectionLimit = downloadOptions.DownloadThreads;

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                string playlistUrl;

                if (downloadOptions.PlaylistUrl == null)
                {
                    Task<JObject> taskAccessToken = TwitchHelper.GetVideoToken(downloadOptions.Id, downloadOptions.Oauth);
                    await taskAccessToken;

                    string[] videoPlaylist = await TwitchHelper.GetVideoPlaylist(downloadOptions.Id, taskAccessToken.Result["data"]["videoPlaybackAccessToken"]["value"].ToString(), taskAccessToken.Result["data"]["videoPlaybackAccessToken"]["signature"].ToString());
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

                    if (videoQualities.Any(x => x.Key.Equals(downloadOptions.Quality)))
                        playlistUrl = videoQualities.Where(x => x.Key.Equals(downloadOptions.Quality)).First().Value;
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
                
                
                using (WebClient client = new WebClient())
                {
                    string[] videoChunks = (await client.DownloadStringTaskAsync(playlistUrl)).Split('\n');

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
                }

                List<KeyValuePair<string, double>> videoListCropped = GenerateCroppedVideoList(videoList, downloadOptions);
                Queue<string> videoParts = new Queue<string>();
                videoListCropped.ForEach(x => videoParts.Enqueue(x.Key));
                List<string> videoPartsList = new List<string>(videoParts);
                int partCount = videoParts.Count;
                int doneCount = 0;

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
                            while (!isDone && errorCount < 10)
                            {
                                try
                                {
                                    using (WebClient client = new WebClient())
                                    {
                                        if (tryUnmute && request.Contains("-muted"))
                                        {
                                            await client.DownloadFileTaskAsync(baseUrl + request.Replace("-muted", ""), Path.Combine(downloadFolder, RemoveQueryString(request)));
                                        }
                                        else
                                        {
                                            await client.DownloadFileTaskAsync(baseUrl + request, Path.Combine(downloadFolder, RemoveQueryString(request)));
                                        }
                                        
                                        isDone = true;
                                    }
                                }
                                catch (WebException ex)
                                {
                                    errorCount++;
                                    Debug.WriteLine(ex);

                                    HttpStatusCode? status = (ex.Response as HttpWebResponse)?.StatusCode;
                                    if (status != null && status == HttpStatusCode.Forbidden)
                                    {
                                        tryUnmute = false;
                                    }
                                    else
                                    {
                                        await Task.Delay(10000);
                                    }
                                }
                            }

                            if (!isDone)
                                throw new Exception("Video part " + request + " failed after 10 retries");

                            doneCount++;
                            int percent = (int)Math.Floor(((double)doneCount / (double)partCount) * 100);
                            progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = String.Format("Downloading {0}% (1/3)", percent) });
                            progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });

                            return;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    })).ToArray();
                    await Task.WhenAll(downloadTasks);
                }

                CheckCancelation(cancellationToken, downloadFolder);

                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Combining Parts (2/3)" });
                progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = 0 });

                await Task.Run(() =>
                {
                    string outputFile = Path.Combine(downloadFolder, "output.ts");
                    using (FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                    {
                        foreach (var part in videoPartsList)
                        {
                            string file = Path.Combine(downloadFolder, RemoveQueryString(part));
                            if (File.Exists(file))
                            {
                                byte[] writeBytes = File.ReadAllBytes(file);
                                outputStream.Write(writeBytes, 0, writeBytes.Length);

                                try
                                {
                                    File.Delete(file);
                                }
                                catch { }
                            }
                            CheckCancelation(cancellationToken, downloadFolder);
                        }
                    }
                });


                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Finalizing MP4 (3/3)" });

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
                                Arguments = String.Format("-y -avoid_negative_ts make_zero " + (downloadOptions.CropBeginning ? "-ss {1} " : "") + "-i \"{0}\" -analyzeduration {2} -probesize {2} " + (downloadOptions.CropEnding ? "-t {3} " : "") + "-c:v copy \"{4}\"", Path.Combine(downloadFolder, "output.ts"), (seekTime - startOffset).ToString(CultureInfo.InvariantCulture), Int32.MaxValue, seekDuration.ToString(CultureInfo.InvariantCulture), Path.GetFullPath(downloadOptions.Filename)),
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardInput = false,
                                RedirectStandardOutput = false,
                                RedirectStandardError = false
                            }
                    };
                    process.Start();
                    process.WaitForExit();
                });
                Cleanup(downloadFolder);
            }
            catch
            {
                Cleanup(downloadFolder);
                throw;
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

        private static void DownloadThread(string videoPart, string baseUrl, string downloadFolder)
        {
            bool isDone = false;
            int errorCount = 0;
            while (!isDone && errorCount < 10)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(baseUrl + videoPart, Path.Combine(downloadFolder, videoPart));
                        isDone = true;
                    }
                }
                catch (WebException)
                {
                    errorCount++;
                    Thread.Sleep(10000);
                }
            }

            if (!isDone)
                throw new Exception("Video part " + videoPart + " failed after 10 retries");
        }
    }
}
