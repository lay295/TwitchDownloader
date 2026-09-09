using System.Runtime.InteropServices;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class FfmpegService
    {
        public static string ExecutableName { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        public string ResolvedPath { get; private set; } = ExecutableName;

        public bool IsAvailable()
        {
            var resolved = ResolvePath();
            if (resolved is null)
                return false;

            ResolvedPath = resolved;
            return true;
        }

        public bool NeedsRefresh()
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, ExecutableName);
            if (!File.Exists(localPath))
                return !IsAvailable();

            return File.GetLastWriteTime(localPath) < DateTime.Now - TimeSpan.FromDays(365);
        }

        public async Task EnsureAvailableAsync(ITaskProgress progress, CancellationToken cancellationToken = default)
        {
            if (IsAvailable() && !NeedsRefresh())
                return;

            var destination = Path.Combine(AppContext.BaseDirectory, ExecutableName);
            using var progressHandler = new XabeProgressHandler(progress);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, AppContext.BaseDirectory, progressHandler);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(destination))
            {
                try
                {
                    TwitchHelper.Set777UnixFilePermissions(new FileInfo(destination));
                }
                catch
                {
                    var chmodCommand = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? "sudo chmod +x ffmpeg"
                        : "chmod +x ffmpeg";

                    progress.LogError(Loc.Get("status.ffmpeg_chmod_failed", chmodCommand));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            IsAvailable();
        }

        private static string? ResolvePath()
        {
            var localPath = Path.Combine(AppContext.BaseDirectory, ExecutableName);
            if (File.Exists(localPath))
                return localPath;

            if (File.Exists(ExecutableName))
                return Path.GetFullPath(ExecutableName);

            return GetFileOnPath(ExecutableName);
        }

        private static string? GetFileOnPath(string fileName)
        {
            var environmentPath = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(environmentPath))
                return null;

            foreach (var path in environmentPath.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private sealed class XabeProgressHandler : IProgress<ProgressInfo>, IDisposable
        {
            private int _lastPercent = -1;
            private readonly ITaskProgress _progress;

            public XabeProgressHandler(ITaskProgress progress)
            {
                _progress = progress;
                _progress.SetTemplateStatus(Loc.Get("status.downloading_ffmpeg"), 0);
            }

            public void Report(ProgressInfo value)
            {
                if (value.TotalBytes <= 0)
                    return;

                var percent = (int)(value.DownloadedBytes / (double)value.TotalBytes * 100);
                if (percent <= _lastPercent)
                    return;

                _lastPercent = percent;
                _progress.ReportProgress(percent);
            }

            public void Dispose()
            {
            }
        }
    }
}
