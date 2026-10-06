using Avalonia.Controls.ApplicationLifetimes;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.Updater.ViewModels
{
    public sealed partial class UpdateWindowViewModel : ViewModelBase
    {
        private readonly UpdateProcessArgs _args;
        private readonly UpdateCheckService _checks;
        private readonly AppUpdateService _installer;
        private readonly GitHubReleaseNotesClient _notes;
        private readonly UpdatePreferencesStore _preferences;
        private readonly bool _verboseErrors;
        private readonly bool _suppressPrefSave;
        private readonly CancellationTokenSource _lifetime = new();

        private string? _downloadUrl;
        private Version? _remoteVersion;
        private Version _localVersion = new(0, 0, 0);
        private UpdatePreferences _prefs;

        public UpdateWindowViewModel(
            LocalizationService loc,
            UpdateProcessArgs args,
            UpdateCheckService checks,
            AppUpdateService installer,
            GitHubReleaseNotesClient notes,
            UpdatePreferencesStore preferences,
            bool verboseErrors) : base(loc)
        {
            _args = args;
            _checks = checks;
            _installer = installer;
            _notes = notes;
            _preferences = preferences;
            _verboseErrors = verboseErrors;
            _prefs = preferences.Load();
            _suppressPrefSave = true;
            AutoOfferUpdates = _prefs.OfferOnStartup;
            _suppressPrefSave = false;
            StatusText = loc.Get("about.checking");
            Heading = loc.Get("updater.heading");
            AppName = loc.Get("common.app_name");
            NotesTitle = loc.Get("updater.notes_title", string.Empty);
            _ = LoadAsync();
        }

        [ObservableProperty]
        public partial string Heading { get; set; }

        [ObservableProperty]
        public partial string AppName { get; set; }

        [ObservableProperty]
        public partial string CurrentVersionLabel { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string RemoteVersionLabel { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasReleasedText))]
        public partial string? ReleasedText { get; set; }

        public bool HasReleasedText => !string.IsNullOrEmpty(ReleasedText);

        [ObservableProperty]
        public partial string NotesTitle { get; set; }

        [ObservableProperty]
        public partial string StatusText { get; set; }

        [ObservableProperty]
        public partial string? ReleaseNotes { get; set; }

        [ObservableProperty]
        public partial string ChangelogUrl { get; set; } = UpdateCheckService.DEFAULT_CHANGELOG_URL;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanInstall))]
        [NotifyPropertyChangedFor(nameof(CanDefer))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        [NotifyCanExecuteChangedFor(nameof(SkipCommand))]
        [NotifyCanExecuteChangedFor(nameof(LaterCommand))]
        public partial bool IsBusy { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanInstall))]
        [NotifyPropertyChangedFor(nameof(CanDefer))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        [NotifyCanExecuteChangedFor(nameof(SkipCommand))]
        [NotifyCanExecuteChangedFor(nameof(LaterCommand))]
        public partial bool IsInstalling { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanInstall))]
        [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
        public partial bool PackageAvailable { get; set; }

        [ObservableProperty]
        public partial bool HasError { get; set; }

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        public partial bool ShowProgress { get; set; }

        [ObservableProperty]
        public partial bool AutoOfferUpdates { get; set; }

        public bool CanInstall => !IsBusy && !IsInstalling && (PackageAvailable || IsDryRun);

        public bool CanDefer => !IsBusy && !IsInstalling;

        public bool IsDryRun => _args.DryRun || AppUpdateService.IsDryRun;

        private bool CanInstallUpdate() => CanInstall;

        private bool CanDeferUpdate() => CanDefer;

        partial void OnAutoOfferUpdatesChanged(bool value)
        {
            if (_suppressPrefSave)
                return;

            _prefs = UpdatePromptPolicy.WithOfferOnStartup(_prefs, value);
            _preferences.Save(_prefs);
        }

        private void NotifyCommands()
        {
            InstallCommand.NotifyCanExecuteChanged();
            SkipCommand.NotifyCanExecuteChanged();
            LaterCommand.NotifyCanExecuteChanged();
        }

        public string Title => IsDryRun
            ? Loc.Get("updater.window_title") + " — DEBUG"
            : Loc.Get("updater.window_title");

        public string Summary => Loc.Get("updater.summary");

        private async Task LoadAsync()
        {
            try
            {
                if (!Version.TryParse(_args.LocalVersion, out var local))
                    local = new Version(0, 0, 0);

                _localVersion = local.StripRevisionIfDefault();
                var result = await _checks.CheckAsync(_localVersion, _lifetime.Token);
                if (result is null)
                {
                    if (IsDryRun)
                    {
                        ShowDryRunPreview();
                        return;
                    }

                    SetError(Loc.Get("updater.notes_unavailable"));
                    return;
                }

                _remoteVersion = result.RemoteVersion;
                ChangelogUrl = result.ChangelogUrl;
                Heading = Loc.Get("updater.heading");
                CurrentVersionLabel = FormatVersion(_localVersion);
                RemoteVersionLabel = FormatVersion(result.RemoteVersion);
                NotesTitle = Loc.Get("updater.notes_title", FormatVersion(result.RemoteVersion));
                StatusText = Loc.Get("about.update_available", result.RemoteVersion);

                var notes = await LoadNotesAsync(result.RemoteVersion.ToString());
                ReleaseNotes = notes?.Body ?? Loc.Get("updater.notes_unavailable");
                ReleasedText = notes?.PublishedAt is { } published
                    ? Loc.Get("updater.released", published.ToLocalTime().ToString("D", CultureInfo.CurrentCulture))
                    : null;

                var url = UpdatePackageResolver.TryResolveDownloadUrlForCurrentOs(result.AvaloniaUrlTemplate, result.RemoteVersion);
                if (url is null && !IsDryRun)
                {
                    PackageAvailable = false;
                    StatusText = Loc.Get("updater.unsupported_platform");
                    return;
                }

                _downloadUrl = url;
                if (!result.IsNewer && !IsDryRun)
                {
                    PackageAvailable = false;
                    Heading = Loc.Get("about.up_to_date");
                    StatusText = Loc.Get("about.up_to_date");
                    return;
                }

                if (IsDryRun)
                {
                    PackageAvailable = true;
                    StatusText = Loc.Get("updater.debug_dry_run");
                    return;
                }

                if (url is null)
                {
                    PackageAvailable = false;
                    StatusText = Loc.Get("updater.unsupported_platform");
                    return;
                }

                var available = await _installer.ProbePackageAsync(url, _lifetime.Token);
                PackageAvailable = available;
                if (!available)
                    StatusText = Loc.Get("updater.no_package");
            }
            catch (Exception ex)
            {
                if (IsDryRun)
                {
                    ShowDryRunPreview();
                    return;
                }

                SetError(FormatError(Loc.Get("updater.notes_unavailable"), ex));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ShowDryRunPreview()
        {
            Heading = Loc.Get("updater.heading");
            CurrentVersionLabel = FormatVersion(_localVersion);
            RemoteVersionLabel = FormatVersion(_localVersion);
            NotesTitle = Loc.Get("updater.notes_title", FormatVersion(_localVersion));
            ReleaseNotes = Loc.Get("updater.debug_dry_run");
            StatusText = Loc.Get("updater.debug_dry_run");
            PackageAvailable = true;
            HasError = false;
        }

        private async Task<GitHubReleaseNotes?> LoadNotesAsync(string tag)
        {
            try
            {
                return await _notes.GetReleaseAsync(tag, _lifetime.Token);
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
        private async Task InstallAsync()
        {
            if (_downloadUrl is null && !IsDryRun)
                return;

            IsInstalling = true;
            ShowProgress = true;
            HasError = false;
            NotifyCommands();

            try
            {
                var progress = new Progress<UpdateInstallProgress>(UpdateProgress);
                var installDir = string.IsNullOrWhiteSpace(_args.InstallDirectory)
                    ? AppContext.BaseDirectory
                    : _args.InstallDirectory;
                var exePath = AppUpdateService.GetMainExecutablePath(installDir);

                StatusText = Loc.Get("updater.downloading");
                await _installer.InstallAsync(_downloadUrl ?? string.Empty, installDir, exePath, new StagingProgress(progress, this), _lifetime.Token);
                if (IsDryRun)
                {
                    StatusText = Loc.Get("updater.debug_dry_run_done");
                    IsInstalling = false;
                    IsBusy = false;
                    NotifyCommands();
                    return;
                }

                StatusText = Loc.Get("updater.restarting");
                RestartMain(installDir);
            }
            catch (InvalidOperationException ex) when (ex.Message is "debug_skip" or "not_writable" or "process_path_missing" or "queue_busy")
            {
                SetError(Loc.Get("updater." + ex.Message));
                IsInstalling = false;
                ShowProgress = false;
                IsBusy = false;
                NotifyCommands();
            }
            catch (Exception ex)
            {
                SetError(FormatError(Loc.Get("updater.install_failed"), ex));
                IsInstalling = false;
                ShowProgress = false;
                IsBusy = false;
                NotifyCommands();
            }
        }

        private void UpdateProgress(UpdateInstallProgress value)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => UpdateProgress(value));
                return;
            }

            Progress = value.Percent;
            StatusText = value.Phase switch
            {
                UpdateInstallPhase.Downloading => Loc.Get("updater.downloading"),
                UpdateInstallPhase.Extracting => Loc.Get("updater.extracting"),
                UpdateInstallPhase.Finishing => Loc.Get("updater.restarting"),
                _ => StatusText,
            };
        }

        [RelayCommand(CanExecute = nameof(CanDeferUpdate))]
        private void Skip()
        {
            if (_remoteVersion is not null)
            {
                _prefs = UpdatePromptPolicy.Skip(_prefs, _remoteVersion);
                _preferences.Save(_prefs);
            }

            Shutdown();
        }

        [RelayCommand(CanExecute = nameof(CanDeferUpdate))]
        private void Later()
        {
            _prefs = UpdatePromptPolicy.RemindLater(_prefs, DateTimeOffset.Now);
            _preferences.Save(_prefs);
            Shutdown();
        }

        [RelayCommand]
        private void OpenChangelog()
        {
            if (string.IsNullOrWhiteSpace(ChangelogUrl))
                return;

            Process.Start(new ProcessStartInfo(ChangelogUrl) { UseShellExecute = true });
        }

        private void SetError(string message)
        {
            HasError = true;
            StatusText = message;
            PackageAvailable = false;
        }

        private string FormatError(string message, Exception ex)
        {
            return _verboseErrors ? $"{message}{Environment.NewLine}{ex}" : message;
        }

        private static string FormatVersion(Version version) => "v" + version;

        private static void Shutdown()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }

        private sealed class StagingProgress(IProgress<UpdateInstallProgress> inner, UpdateWindowViewModel owner)
            : IProgress<UpdateInstallProgress>
        {
            private bool _closedMain;

            public void Report(UpdateInstallProgress value)
            {
                if (!_closedMain && value.Phase is UpdateInstallPhase.Extracting)
                {
                    _closedMain = true;
                    owner.CloseMainForInstall();
                }

                inner.Report(value);
            }
        }

        internal void CloseMainForInstall()
        {
            if (IsDryRun)
            {
                Dispatcher.UIThread.Post(() => StatusText = Loc.Get("updater.closing_app"));
                return;
            }
            Dispatcher.UIThread.Post(() => StatusText = Loc.Get("updater.closing_app"));
            if (_args.ParentPid > 0)
            {
                UpdateShutdownSignal.Signal(_args.ParentPid);
                try
                {
                    using var process = Process.GetProcessById(_args.ParentPid);
                    if (!process.WaitForExit(60_000))
                        throw new InvalidOperationException("queue_busy");
                }
                catch (ArgumentException)
                {
                    // Already exited.
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch
                {
                    throw new InvalidOperationException("queue_busy");
                }
            }
        }

        private void RestartMain(string installDir)
        {
            var exe = AppUpdateService.GetMainExecutablePath(installDir);
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = installDir,
                });
            }

            Shutdown();
        }
    }
}
