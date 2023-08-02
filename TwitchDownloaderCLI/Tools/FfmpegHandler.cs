using Mono.Unix;
using System;
using System.IO;
using System.Runtime.InteropServices;
using TwitchDownloaderCLI.Modes.Arguments;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TwitchDownloaderCLI.Tools
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

            var progressHandler = new Progress<ProgressInfo>();
            progressHandler.ProgressChanged += XabeProgressHandler.OnProgressReceived;

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
                Console.WriteLine("[ERROR] - Unable to update FFmpeg file permissions. Run 'sudo chmod +x ffmpeg' if further FFmpeg errors occur.");
            }
        }

        public static void DetectFfmpeg(string ffmpegPath)
        {
            if (File.Exists(ffmpegPath) || File.Exists(FfmpegExecutableName) || PathUtils.ExistsOnPATH(FfmpegExecutableName))
            {
                return;
            }

            Console.WriteLine("[ERROR] - Unable to find FFmpeg, exiting. You can download FFmpeg automatically with the command \"TwitchDownloaderCLI ffmpeg -d\"");
            Environment.Exit(1);
        }
    }

    internal static class XabeProgressHandler
    {
        internal static void OnProgressReceived(object sender, ProgressInfo e)
        {
            var percent = (int)(e.DownloadedBytes / (double)e.TotalBytes * 100);
            Console.Write($"\r[INFO] - Downloading FFmpeg {percent}%");
        }
    }
}