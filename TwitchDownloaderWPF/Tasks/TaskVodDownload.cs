using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Model;

namespace TwitchDownloader.Tasks
{
    public class TaskVodDownload : ITwitchTask
    {
        public CancellationTokenSource CancellationTokenSource
        {
            get;
            set;
        }
        public string Title { get; set; }
        public string Information { get; set; }
        public ImageSource Preview { get; set; }

        public DownloadOptions downloadOptions;

        public void cancelTask()
        {
            CancellationTokenSource.Cancel();
        }

        public Task runTask(IProgress<ProgressReport> progress)
        {
            CancellationTokenSource = new CancellationTokenSource();

            DownloadOptions options = downloadOptions;
            List<Thread> threads = new List<Thread>();
            int downloadThreads = options.download_threads;
            string tempFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, options.id.ToString());
            ServicePointManager.DefaultConnectionLimit = downloadThreads;

            if (Directory.Exists(downloadFolder))
                DeleteDirectory(downloadFolder);
            Directory.CreateDirectory(downloadFolder);

            string playlistUrl = "";
            foreach (var item in options.video_qualities)
            {
                if (item.Key == options.quality)
                {
                    playlistUrl = item.Value;
                }
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
            Queue<string> videoParts = new Queue<string>(GenerateCroppedVideoList(videoList, options));
            List<string> videoPartsList = new List<string>(videoParts);
            int partCount = videoParts.Count;
            int doneCount = 0;

            Parallel.ForEach(videoParts, new ParallelOptions { MaxDegreeOfParallelism = downloadThreads },
            videoPart =>
            {
                DownloadThread(videoPart, baseUrl, downloadFolder);
                doneCount++;
                int percent = (int)Math.Floor(((double)doneCount / (double)partCount) * 100);
                progress.Report(new ProgressReport() {reportType = ReportType.Message, data = String.Format("Downloading {0}% (1/3)", percent) });
                progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });
            });

            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Combining Parts (2/3)" });
            progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = 0 });

            /*
            if (videoPartsList[0].Contains("muted"))
            {
                string inputFileMuted = Path.Combine(downloadFolder, videoPartsList[0]);
                string outputFileMuted = Path.Combine(downloadFolder, "new_" + videoPartsList[0]);
                Task<IConversionResult> unmuteResult = new Conversion().Start(String.Format("-f lavfi -i anullsrc -i \"{0}\" -shortest -c:v copy -c:a aac -map 0:a -map 1:v \"{1}\"", inputFileMuted, outputFileMuted));
                Task.WaitAll(unmuteResult);
                videoPartsList[0] = "new_" + videoPartsList[0];
            }

            using (StreamWriter file = new StreamWriter(Path.Combine(downloadFolder, options.id + ".txt"), false))
            {
                foreach (var part in videoPartsList)
                {
                    file.WriteLine(String.Format("file '{0}'", Path.Combine(downloadFolder, part)));
                }
            }

            string inputFile = Path.Combine(downloadFolder, options.id + ".txt");
            Task<IConversionResult> combineResult = new Conversion().Start(String.Format("-f concat -safe 0 -i \"{0}\" -c copy \"{1}\"", inputFile, outputFile));
            Task.WaitAll(combineResult);*/

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
                }
            }

            bool isVFR = false;
            if (options.encode_cfr)
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = "ffmpeg.exe",
                        Arguments = $"-i \"" + Path.Combine(downloadFolder, "output.mp4") + "\" -vf vfrdet -ss 0 -t 600 -f null -",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                string output = "";
                process.ErrorDataReceived += delegate (object o, DataReceivedEventArgs args)
                {
                    if (args.Data != null && args.Data.Contains("Parsed_vfrdet"))
                    {
                        output += args.Data;
                    }
                };
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
                double VFR = Double.Parse(output.Substring(output.IndexOf("VFR:") + 4, 8));
                if (VFR == 0.0)
                {
                    progress.Report(new ProgressReport() { reportType = ReportType.Log, data = "Constant framerate detected, no need to re-encode" });
                }
                else
                {
                    isVFR = true;
                    progress.Report(new ProgressReport() { reportType = ReportType.Log, data = "Re-encoding to CFR, this will take a while and is only needed if you're having desync issues in Adobe Premiere with this channel" });
                }
            }

            if (isVFR)
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Re-encoding MP4 (3/3)" });
            else
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Finalizing MP4 (3/3)" });
            string outputConvert = options.filename;
            Task<IMediaInfo> info = MediaInfo.Get(Path.Combine(downloadFolder, "output.mp4"));
            Task.WaitAll(info);
            double seekTime = options.crop_begin;
            double seekDuration = Math.Round(info.Result.Duration.TotalSeconds - seekTime - options.crop_end);
            Task<IConversionResult> conversionResult = null;
            if (isVFR)
            {
                int newFps = (int)Math.Ceiling(info.Result.VideoStreams.First().FrameRate);
                conversionResult = Conversion.New().Start(String.Format("-y -i \"{0}\" -ss {1} -analyzeduration {2} -t {3} -crf 20 -filter:v fps=fps={4} \"{5}\"", Path.Combine(downloadFolder, "output.mp4"), seekTime.ToString(), Int32.MaxValue, seekDuration.ToString(), newFps, outputConvert));
            }
            else
            {
                conversionResult = Conversion.New().Start(String.Format("-y -i \"{0}\" -ss {1} -analyzeduration {2} -t {3} -avoid_negative_ts make_zero -c:v copy -f mp4 \"-bsf:a\" aac_adtstoasc \"{4}\"", Path.Combine(downloadFolder, "output.mp4"), seekTime.ToString(), Int32.MaxValue, seekDuration.ToString(), outputConvert));
            }

            Task.WaitAll(conversionResult);
            if (Directory.Exists(downloadFolder))
                DeleteDirectory(downloadFolder);

            return Task.CompletedTask;
        }

        private void Cleanup()
        {

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

        private List<string> GenerateCroppedVideoList(List<KeyValuePair<string, double>> videoList, DownloadOptions options)
        {
            double beginTime = 0.0;
            double endTime = 0.0;

            TimeSpan startCrop = options.cropped_begin_time;
            TimeSpan endCrop = options.cropped_end_time;

            foreach (var video in videoList)
                endTime += video.Value;

            if (options.cropped_begin)
            {
                for (int i = 0; i < videoList.Count; i++)
                {
                    if (beginTime + videoList[i].Value <= startCrop.TotalSeconds)
                    {
                        beginTime += videoList[i].Value;
                        videoList.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        options.crop_begin = (beginTime + videoList[i].Value) - startCrop.TotalSeconds;
                        options.crop_begin = videoList[i].Value - options.crop_begin;
                        break;
                    }
                }
            }

            if (options.cropped_end)
            {
                for (int i = videoList.Count - 1; i >= 0; i--)
                {
                    if (endTime - videoList[i].Value >= endCrop.TotalSeconds)
                    {
                        endTime -= videoList[i].Value;
                        videoList.RemoveAt(i);
                    }
                    else
                    {
                        options.crop_end = endCrop.TotalSeconds - (endTime - videoList[i].Value);
                        options.crop_end = videoList[i].Value - options.crop_end;
                        break;
                    }
                }
            }
            List<string> result = new List<string>();
            foreach (var video in videoList)
                result.Add(video.Key);

            return result;
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        public TaskVodDownload(DownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
            Title = downloadOptions.title;
            Information = "This is a test description :)";
        }
    }
}
