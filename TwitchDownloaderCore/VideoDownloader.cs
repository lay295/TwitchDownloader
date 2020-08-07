using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
            string tempFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, downloadOptions.Id.ToString() == "0" ? Guid.NewGuid().ToString() : "");

            ServicePointManager.DefaultConnectionLimit = downloadOptions.DownloadThreads;

            if (Directory.Exists(downloadFolder))
                Directory.Delete(downloadFolder, true);
            Directory.CreateDirectory(downloadFolder);

            string playlistUrl;

            if (downloadOptions.PlaylistUrl == null)
            {
                Task<JObject> taskInfo = TwitchHelper.GetVideoInfo(downloadOptions.Id);
                Task<JObject> taskAccessToken = TwitchHelper.GetVideoToken(downloadOptions.Id, downloadOptions.Oauth);
                await Task.WhenAll(taskInfo, taskAccessToken);

                string[] videoPlaylist = await TwitchHelper.GetVideoPlaylist(downloadOptions.Id, taskAccessToken.Result["token"].ToString(), taskAccessToken.Result["sig"].ToString());
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
            using (WebClient client = new WebClient())
            {
                string[] videoChunks = client.DownloadString(playlistUrl).Split('\n');

                for (int i = 0; i < videoChunks.Length; i++)
                {
                    if (videoChunks[i].Contains("#EXTINF"))
                        videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 1], Double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture)));
                }
            }

            Queue<string> videoParts = new Queue<string>(GenerateCroppedVideoList(videoList, downloadOptions));
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
                        int errorCount = 0;
                        while (!isDone && errorCount < 10)
                        {
                            try
                            {
                                using (WebClient client = new WebClient())
                                {
                                    await client.DownloadFileTaskAsync(baseUrl + request, Path.Combine(downloadFolder, request));
                                    isDone = true;
                                }
                            }
                            catch (WebException)
                            {
                                errorCount++;
                                await Task.Delay(10000);
                            }
                        }

                        if (!isDone)
                            throw new Exception("Video part " + request + " failed after 10 retries");

                        doneCount++;
                        int percent = (int)Math.Floor(((double)doneCount / (double)partCount) * 100);
                        progress.Report(new ProgressReport() { reportType = ReportType.Message, data = String.Format("Downloading {0}% (1/3)", percent) });
                        progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });

                        return;
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

            string outputFile = Path.Combine(downloadFolder, "output.mp4");
            using (FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            {
                foreach (var part in videoPartsList)
                {
                    string file = Path.Combine(downloadFolder, part);
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

            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Finalizing MP4 (3/3)" });

            double seekTime = downloadOptions.CropBeginningTime;
            double seekDuration = Math.Round(downloadOptions.CropEndingTime - seekTime);
            
            try
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.GetFullPath(downloadOptions.FfmpegPath),
                        Arguments = String.Format("-y -i \"{0}\" -ss {1} -analyzeduration {2} " + (downloadOptions.CropEnding ? "-t {3}" : "") + " -avoid_negative_ts make_zero -c:v copy -f mp4 \"-bsf:a\" aac_adtstoasc \"{4}\"", Path.Combine(downloadFolder, "output.mp4"), seekTime.ToString(), Int32.MaxValue, seekDuration.ToString(), Path.GetFullPath(downloadOptions.Filename)),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            catch (TaskCanceledException) { }

            Cleanup(downloadFolder);
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

        private List<string> GenerateCroppedVideoList(List<KeyValuePair<string, double>> videoList, VideoDownloadOptions downloadOptions)
        {
            double beginTime = 0.0;
            double endTime = 0.0;

            TimeSpan startCrop = TimeSpan.FromSeconds(downloadOptions.CropBeginningTime);
            TimeSpan endCrop = TimeSpan.FromSeconds(downloadOptions.CropEndingTime);

            foreach (var video in videoList)
                endTime += video.Value;

            if (downloadOptions.CropBeginning)
            {
                for (int i = 0; i < videoList.Count; i++)
                {
                    if (beginTime + videoList[i].Value <= startCrop.TotalSeconds)
                    {
                        beginTime += videoList[i].Value;
                        videoList.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (downloadOptions.CropEnding)
            {
                for (int i = videoList.Count - 1; i >= 0; i--)
                {
                    if (endTime - videoList[i].Value >= endCrop.TotalSeconds)
                    {
                        endTime -= videoList[i].Value;
                        videoList.RemoveAt(i);
                    }
                }
            }
            List<string> result = new List<string>();
            foreach (var video in videoList)
                result.Add(video.Key);

            return result;
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
