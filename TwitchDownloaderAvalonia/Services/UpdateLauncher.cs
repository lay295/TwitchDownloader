using Avalonia.Controls.ApplicationLifetimes;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class UpdateLauncher(
        LocalizationService loc,
        IDialogService dialogs,
        QueueService queue,
        UpdatePreferencesStore preferences)
    {
        private Process? _updater;
        private CancellationTokenSource? _shutdownWatch;

        public async Task LaunchAsync(Version? remoteVersion = null, bool force = false)
        {
            var prefs = preferences.Load();
            if (!force
                && remoteVersion is not null
                && !UpdatePromptPolicy.ShouldPrompt(remoteVersion, prefs, DateTimeOffset.Now))
            {
                return;
            }

            if (queue.HasUnfinishedWork)
            {
                await dialogs.ShowMessageAsync(loc.Get("updater.title"), loc.Get("updater.queue_busy"));
                return;
            }

            if (_updater is { HasExited: false })
                return;

            var updaterPath = FindUpdaterExecutable();
            if (updaterPath is null)
            {
                await dialogs.ShowMessageAsync(loc.Get("updater.title"), loc.Get("updater.missing_updater"));
                return;
            }

            var pid = Environment.ProcessId;
            string staged;
            try
            {
                UpdateShutdownSignal.Clear(pid);
                staged = AppUpdateService.IsDryRun ? updaterPath : StageUpdater(updaterPath, pid);
            }
            catch (Exception ex)
            {
                await dialogs.ShowErrorAsync(loc.Get("updater.title"), loc.Get("updater.install_failed") + Environment.NewLine + ex.Message);
                return;
            }

            var localVersion = typeof(UpdateLauncher).Assembly.GetName().Version?.StripRevisionIfDefault() ?? new Version(0, 0, 0);
            var args = new UpdateProcessArgs(
                pid,
                Path.GetFullPath(AppContext.BaseDirectory),
                localVersion.ToString(),
                DryRun: AppUpdateService.IsDryRun,
                Culture: loc.Culture);

            var start = new ProcessStartInfo(staged)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(staged)!,
            };

            foreach (var argument in args.ToArguments())
                start.ArgumentList.Add(argument);

            try
            {
                _updater = Process.Start(start);
            }
            catch (Exception ex)
            {
                await dialogs.ShowErrorAsync(loc.Get("updater.title"), loc.Get("updater.install_failed") + Environment.NewLine + ex.Message);
                return;
            }

            if (AppUpdateService.IsDryRun)
                return;

            _shutdownWatch?.Cancel();
            _shutdownWatch = new CancellationTokenSource();
            _ = WatchShutdownAsync(pid, _shutdownWatch.Token);
        }

        private async Task WatchShutdownAsync(int pid, CancellationToken cancellationToken)
        {
            try
            {
                await UpdateShutdownSignal.WaitAsync(pid, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (queue.HasUnfinishedWork)
                    return;

                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            });
        }

        private static string StageUpdater(string updaterPath, int pid)
        {
            var destDir = Path.Combine(Path.GetTempPath(), "TwitchDownloaderAvalonia.Updater", pid.ToString());
            Directory.CreateDirectory(destDir);
            var dest = Path.Combine(destDir, UpdateHost.UpdaterExecutableFileName);
            File.Copy(updaterPath, dest, overwrite: true);

            if (OperatingSystem.IsWindows())
                return dest;

            try
            {
                var info = new FileInfo(dest);
                info.UnixFileMode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            }
            catch
            {
                // Extracted copy is still attempted; user can chmod if launch fails.
            }

            return dest;
        }

        internal static string? FindUpdaterExecutable()
        {
            var fileName = UpdateHost.UpdaterExecutableFileName;
            foreach (var candidate in EnumerateUpdaterCandidates(fileName))
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static IEnumerable<string> EnumerateUpdaterCandidates(string fileName)
        {
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
            yield return Path.Combine(AppContext.BaseDirectory, "updater", fileName);

            var config =
#if DEBUG
                "Debug";
#else
                "Release";
#endif
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                yield return Path.Combine(dir.FullName, "TwitchDownloaderAvalonia.Updater", "bin", config, "net10.0", fileName);
        }
    }
}
