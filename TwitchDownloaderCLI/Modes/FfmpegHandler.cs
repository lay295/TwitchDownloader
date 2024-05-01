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
    internal static class FfmpegHandler
    {
        public static readonly string FfmpegExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        public static void ParseArgs(FfmpegArgs args)
        {
            var progress = new CliTaskProgress(args.LogLevel);

            if (args.DownloadFfmpeg)
            {
                DownloadFfmpeg(progress);
            }
        }

        private static void DownloadFfmpeg(ITaskProgress progress)
        {
            if (File.Exists(FfmpegExecutableName))
            {
                progress.LogInfo("FFmpeg was already found in the current working directory.");
                return;
            }

            using var progressHandler = new XabeProgressHandler(progress);

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
                var chmodCommand = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "chmod +x ffmpeg" : "sudo chmod +x ffmpeg";
                progress.LogError($"Unable to update FFmpeg file permissions. Run '{chmodCommand}' if further FFmpeg errors occur.");
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

            // This may seem overly complicated, but it removes the expensive console writes from the thread that is downloading FFmpeg
            public XabeProgressHandler(ITaskProgress progress)
            {
                progress.SetTemplateStatus("Downloading FFmpeg {0}%", 0);
                _timer = new Timer(_ =>
                {
                    progress.ReportProgress(_percentQueue.Max());
                }, null, 0, 100);
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