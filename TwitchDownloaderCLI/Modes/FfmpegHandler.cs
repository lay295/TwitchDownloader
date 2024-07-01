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

namespace TwitchDownloaderCLI.Modes;

internal static class FfmpegHandler {
    public static readonly string FfmpegExecutableName
        = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

    public static void ParseArgs(FfmpegArgs args) {
        var progress = new CliTaskProgress(args.LogLevel);

        if (args.DownloadFfmpeg)
            DownloadFfmpeg(progress);
    }

    private static void DownloadFfmpeg(ITaskProgress progress) {
        if (File.Exists(FfmpegHandler.FfmpegExecutableName)) {
            progress.LogInfo("FFmpeg was already found in the current working directory.");
            return;
        }

        using var progressHandler = new XabeProgressHandler(progress);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full, progressHandler).GetAwaiter().GetResult();
            return;
        }

        FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, progressHandler).GetAwaiter().GetResult();

        Console.WriteLine();

        try {
            var ffmpegFileInfo = new UnixFileInfo("ffmpeg") {
                FileAccessPermissions = FileAccessPermissions.UserRead
                    | FileAccessPermissions.UserWrite
                    | FileAccessPermissions.GroupRead
                    | FileAccessPermissions.OtherRead
                    | FileAccessPermissions.UserExecute
                    | FileAccessPermissions.GroupExecute
                    | FileAccessPermissions.OtherExecute
            };
            ffmpegFileInfo.Refresh();
        } catch {
            var chmodCommand = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "chmod +x ffmpeg"
                : "sudo chmod +x ffmpeg";
            progress.LogError(
                $"Unable to automatically update FFmpeg file permissions. Please run \"{chmodCommand}\" to allow FFmpeg to be executed."
            );
        }
    }

    public static void DetectFfmpeg(string ffmpegPath, ITaskLogger logger) {
        if (File.Exists(ffmpegPath)
            || File.Exists(FfmpegHandler.FfmpegExecutableName)
            || PathUtils.ExistsOnPATH(FfmpegHandler.FfmpegExecutableName))
            return;

        var processFileName = Path.GetFileName(Environment.ProcessPath);
        logger.LogError(
            $"Unable to find FFmpeg, exiting. You can download FFmpeg automatically with the command \"{processFileName} ffmpeg -d\""
        );
        Environment.Exit(1);
    }

    private sealed class XabeProgressHandler : IProgress<ProgressInfo>, IDisposable {
        private readonly ConcurrentQueue<int> _percentQueue = new();
        private readonly Timer _timer;
        private int _lastPercent = -1;

        // This may seem overly complicated, but it removes the expensive console writes from the thread that is downloading FFmpeg
        public XabeProgressHandler(ITaskProgress progress) {
            progress.SetTemplateStatus("Downloading FFmpeg {0}%", 0);
            this._timer = new(
                _ => {
                    if (this._percentQueue.IsEmpty) return;

                    progress.ReportProgress(this._percentQueue.Max());
                },
                null,
                0,
                100
            );
        }

        public void Dispose() {
            this._timer?.Dispose();
            this._percentQueue.Clear();
        }

        public void Report(ProgressInfo value) {
            var percent = (int)(value.DownloadedBytes / (double)value.TotalBytes * 100);

            if (percent <= this._lastPercent)
                return;

            this._lastPercent = percent;
            this._percentQueue.Enqueue(percent);
        }
    }
}
