using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Mono.Unix;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Interfaces;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TwitchDownloaderCLI.Modes
{
    public static class FfmpegHandler
    {
        public static readonly string FfmpegExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        public static void ParseArgs(FfmpegArgs args)
        {
            if (args.DownloadFfmpeg)
            {
                DownloadFfmpeg();
            }
        }

        private static void DownloadFfmpeg()
        {
            Console.Write("[INFO] - Downloading FFmpeg");

            using var progressHandler = new XabeProgressHandler();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full, progressHandler).GetAwaiter().GetResult();
                return;
            }

            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, progressHandler).GetAwaiter().GetResult();

            Console.WriteLine();

            try
            {
                var ffmpegFileInfo = new UnixFileInfo("ffmpeg")
                {
                    FileAccessPermissions = FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite | FileAccessPermissions.GroupRead | FileAccessPermissions.OtherRead |
                                            FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute
                };
                ffmpegFileInfo.Refresh();
            }
            catch
            {
                Console.WriteLine("[ERROR] - Unable to update FFmpeg file permissions. Run '{0}' if further FFmpeg errors occur.",
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "chmod +x ffmpeg" : "sudo chmod +x ffmpeg");
            }
        }

        public static void DetectFfmpeg(string ffmpegPath, ITaskLogger logger)
        {
            if (File.Exists(ffmpegPath) || File.Exists(FfmpegExecutableName) || PathUtils.ExistsOnPATH(FfmpegExecutableName))
            {
                return;
            }

            logger.LogError("Unable to find FFmpeg, exiting. You can download FFmpeg automatically with the command \"TwitchDownloaderCLI ffmpeg -d\"");
            Environment.Exit(1);
        }

        private sealed class XabeProgressHandler : IProgress<ProgressInfo>, IDisposable
        {
            private int _lastPercent = -1;
            private readonly ConcurrentQueue<int> _percentQueue = new();
            private readonly Timer _timer;

            public XabeProgressHandler()
            {
                _timer = new Timer(Callback, _percentQueue, 0, 100);

                static void Callback(object state)
                {
                    if (state is not ConcurrentQueue<int> { IsEmpty: false } queue) return;

                    var currentPercent = queue.Max();
                    Console.Write($"\r[INFO] - Downloading FFmpeg {currentPercent}%");
                }
            }

            public void Report(ProgressInfo value)
            {
                var percent = (int)(value.DownloadedBytes / (double)value.TotalBytes * 100);

                if (percent > _lastPercent)
                {
                    _lastPercent = percent;
                    _percentQueue.Enqueue(percent);
                }
            }

            public void Dispose()
            {
                _timer?.Dispose();
                _percentQueue.Clear();
            }
        }
    }
}