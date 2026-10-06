using System.IO.Compression;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.Update.Services
{
    public sealed class AppUpdateService(HttpClient httpClient)
    {
        private readonly Dictionary<string, bool> _probeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Lock _probeLock = new();

        public static bool IsSelfUpdateSupported
        {
            get
            {
#if DEBUG
                return false;
#else
                return true;
#endif
            }
        }

        public static bool IsDryRun => !IsSelfUpdateSupported;

        public async Task<bool> ProbePackageAsync(string url, CancellationToken cancellationToken = default)
        {
            lock (_probeLock)
            {
                if (_probeCache.TryGetValue(url, out var cached))
                    return cached;
            }

            var available = await ProbeCoreAsync(url, cancellationToken);
            lock (_probeLock)
                _probeCache[url] = available;

            return available;
        }

        public async Task InstallAsync(
            string downloadUrl,
            string installDirectory,
            string currentExePath,
            IProgress<UpdateInstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (IsDryRun)
            {
                await SimulateInstallAsync(progress, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(currentExePath))
                throw new InvalidOperationException("process_path_missing");

            if (!IsDirectoryWritable(installDirectory))
                throw new InvalidOperationException("not_writable");

            var archiveName = downloadUrl.AsSpan().GetNthOccurrence('/', ^1).ToString();
            if (string.IsNullOrWhiteSpace(archiveName))
                archiveName = "update.zip";

            var archivePath = Path.Combine(Path.GetTempPath(), "TwitchDownloaderAvalonia.Update", archiveName);
            Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

            var backedUp = false;
            var backupPath = currentExePath + ".bak";

            try
            {
                await DownloadUpdateArchive(downloadUrl, archivePath, progress, cancellationToken).ConfigureAwait(false);

                var previousPermissions = GetUnixFilePermissions(currentExePath);
                BackupCurrentExecutable(currentExePath, backupPath);
                backedUp = File.Exists(backupPath);

                cancellationToken.ThrowIfCancellationRequested();
                ExtractZipFiles(archivePath, installDirectory, progress);

                TryDelete(archivePath);
                ApplyUnixFilePermissions(currentExePath, previousPermissions);
                progress?.Report(new UpdateInstallProgress(100, UpdateInstallPhase.Finishing));
            }
            catch
            {
                if (backedUp)
                    TryRestoreBackup(backupPath, currentExePath);

                TryDelete(archivePath);
                throw;
            }
        }

        public static async Task SimulateInstallAsync(IProgress<UpdateInstallProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            await ReportPhaseAsync(progress, UpdateInstallPhase.Downloading, cancellationToken).ConfigureAwait(false);
            await ReportPhaseAsync(progress, UpdateInstallPhase.Extracting, cancellationToken).ConfigureAwait(false);
            progress?.Report(new UpdateInstallProgress(100, UpdateInstallPhase.Finishing));
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        public static string GetMainExecutablePath(string installDirectory)
        {
            return Path.Combine(installDirectory, UpdateHost.MainExecutableFileName);
        }

        public static bool IsDirectoryWritable(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var probePath = Path.Combine(directory, $".td-write-probe-{Guid.NewGuid():N}");
                File.WriteAllText(probePath, "ok");
                File.Delete(probePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ProbeCoreAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadUpdateArchive(
            string url,
            string archivePath,
            IProgress<UpdateInstallProgress>? progress,
            CancellationToken cancellationToken)
        {
            progress?.Report(new UpdateInstallProgress(0, UpdateInstallPhase.Downloading));

            using var response = await httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var archiveLength = response.Content.Headers.ContentLength;

            await using (var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await contentStream.ProgressCopyToAsync(
                    fs,
                    archiveLength,
                    new Progress<StreamCopyProgress>(p =>
                    {
                        var percent = p.SourceLength <= 0 ? 0 : p.BytesCopied / (double)p.SourceLength * 100;
                        progress?.Report(new UpdateInstallProgress(percent, UpdateInstallPhase.Downloading));
                    }),
                    cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(new UpdateInstallProgress(100, UpdateInstallPhase.Downloading));
        }

        internal static void ExtractZipFiles(string archivePath, string destinationDir, IProgress<UpdateInstallProgress>? progress)
        {
            progress?.Report(new UpdateInstallProgress(0, UpdateInstallPhase.Extracting));
            var destRoot = Path.GetFullPath(destinationDir) + Path.DirectorySeparatorChar;

            using var archiveFs = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(archiveFs, ZipArchiveMode.Read);

            var entryCount = Math.Max(archive.Entries.Count, 1);
            var extracted = 0;

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.FullName) || entry.FullName.Contains("..", StringComparison.Ordinal))
                    continue;

                var destinationPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
                if (!destinationPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase)
                    && !destinationPath.Equals(Path.GetFullPath(destinationDir), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    var parent = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(parent))
                        Directory.CreateDirectory(parent);

                    // ReSharper disable once MethodHasAsyncOverload — ExtractToFileAsync is extremely slow
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }

                extracted++;
                progress?.Report(new UpdateInstallProgress(extracted / (double)entryCount * 100, UpdateInstallPhase.Extracting));
            }

            progress?.Report(new UpdateInstallProgress(100, UpdateInstallPhase.Extracting));
        }

        internal static void BackupCurrentExecutable(string currentExePath, string backupPath)
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            if (!File.Exists(currentExePath))
                return;

            File.Move(currentExePath, backupPath);
        }

        internal static void TryRestoreBackup(string backupPath, string currentExePath)
        {
            try
            {
                if (!File.Exists(backupPath))
                    return;

                if (File.Exists(currentExePath))
                    File.Delete(currentExePath);

                File.Move(backupPath, currentExePath);
            }
            catch
            {
                // Best-effort restore after a failed extract.
            }
        }

        private static async Task ReportPhaseAsync(
            IProgress<UpdateInstallProgress>? progress,
            UpdateInstallPhase phase,
            CancellationToken cancellationToken)
        {
            for (var step = 0; step <= 10; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new UpdateInstallProgress(step * 10, phase));
                await Task.Delay(70, cancellationToken).ConfigureAwait(false);
            }
        }

        private static UnixFileMode? GetUnixFilePermissions(string currentExePath)
        {
            if (OperatingSystem.IsWindows() || !File.Exists(currentExePath))
                return null;

            try
            {
                return new FileInfo(currentExePath).UnixFileMode;
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyUnixFilePermissions(string currentExePath, UnixFileMode? previousPermissions)
        {
            if (OperatingSystem.IsWindows() || !previousPermissions.HasValue || !File.Exists(currentExePath))
                return;

            try
            {
                var info = new FileInfo(currentExePath);
                info.UnixFileMode = previousPermissions.Value;
            }
            catch
            {
                TryMarkUnixExecutable(currentExePath);
            }
        }

        private static void TryMarkUnixExecutable(string currentExePath)
        {
            if (OperatingSystem.IsWindows() || !File.Exists(currentExePath))
                return;

            try
            {
                var info = new FileInfo(currentExePath);
                info.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            }
            catch
            {
                // Caller can chmod manually.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
