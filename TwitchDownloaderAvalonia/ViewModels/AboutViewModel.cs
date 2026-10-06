using System.Reflection;
using System.Runtime.InteropServices;
using TwitchDownloaderAvalonia.Update.Services;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class AboutViewModel : ViewModelBase
    {
        private const string LICENSE_RESOURCE_NAME = "TwitchDownloaderAvalonia.LICENSE.txt";
        private const string DEFAULT_CHANGELOG_URL = UpdateCheckService.DEFAULT_CHANGELOG_URL;

        private readonly UpdateCheckService _updates;
        private readonly IDialogService _dialogs;
        private readonly FfmpegService _ffmpeg;
        private readonly UpdateLauncher _launcher;
        private readonly Version _localVersion;
        private Task? _checkTask;

        public Version? RemoteVersion { get; private set; }

        public AboutViewModel(LocalizationService loc, UpdateCheckService updates, IDialogService dialogs, FfmpegService ffmpeg, UpdateLauncher launcher)
            : base(loc)
        {
            _updates = updates;
            _dialogs = dialogs;
            _ffmpeg = ffmpeg;
            _launcher = launcher;

            var assembly = typeof(AboutViewModel).Assembly;
            _localVersion = assembly.GetName().Version?.StripRevisionIfDefault() ?? new Version(0, 0, 0);
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? Loc.Get("about.description");
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? Loc.Get("about.copyright");
            LicenseText = ReadLicense(assembly);
            ChangelogUrl = DEFAULT_CHANGELOG_URL;
            RuntimeSummary = BuildRuntimeSummary();
        }

        public string AppName => Loc.Get("common.app_name");
        public string VersionText => FormatVersion(_localVersion);
        public string Description => NonEmpty(Loc.Get("about.description"), field);
        public string Copyright => NonEmpty(Loc.Get("about.copyright"), field);
        public string LicenseText { get; }
        public string FrontendNote => Loc.Get("about.frontend_note");

        [ObservableProperty]
        public partial string RuntimeSummary { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateStatus))]
        public partial string? UpdateStatusText { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateStatus))]
        public partial bool IsCheckingUpdate { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateStatus))]
        [NotifyPropertyChangedFor(nameof(ShowUpdateButton))]
        public partial bool HasUpdate { get; set; }

        [ObservableProperty]
        public partial string ChangelogUrl { get; set; }

        public bool ShowUpdateStatus => IsCheckingUpdate || !string.IsNullOrEmpty(UpdateStatusText) || AppUpdateService.IsDryRun;
        public bool ShowUpdateButton => HasUpdate || AppUpdateService.IsDryRun;

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            RuntimeSummary = BuildRuntimeSummary();
            Notify(nameof(AppName), nameof(VersionText), nameof(Description), nameof(FrontendNote), nameof(Copyright));

            if (IsCheckingUpdate)
                UpdateStatusText = Loc.Get("about.checking");
            else if (HasUpdate && RemoteVersion is not null)
                UpdateStatusText = Loc.Get("about.update_available", RemoteVersion);
            else if (!string.IsNullOrEmpty(UpdateStatusText))
                UpdateStatusText = Loc.Get("about.up_to_date");
        }

        public Task EnsureUpdateCheckAsync()
        {
            RuntimeSummary = BuildRuntimeSummary();
            return _checkTask ??= CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            IsCheckingUpdate = true;
            UpdateStatusText = Loc.Get("about.checking");
            HasUpdate = false;

            try
            {
                var result = await _updates.CheckAsync(_localVersion);
                if (result is null)
                {
                    UpdateStatusText = null;
                    _checkTask = null;
                    return;
                }

                ChangelogUrl = result.ChangelogUrl;
                if (result.IsNewer)
                {
                    HasUpdate = true;
                    RemoteVersion = result.RemoteVersion;
                    UpdateStatusText = Loc.Get("about.update_available", RemoteVersion);
                    return;
                }

                UpdateStatusText = Loc.Get("about.up_to_date");
            }
            catch
            {
                UpdateStatusText = null;
                HasUpdate = false;
                _checkTask = null;
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        [RelayCommand]
        private Task OpenUpdateAsync() => _launcher.LaunchAsync(force: true);

        [RelayCommand]
        private void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        [RelayCommand]
        private Task CopyDebugInfoAsync() => _dialogs.CopyTextAsync(BuildDebugInfo());

        private string BuildDebugInfo()
        {
            var ffmpeg = _ffmpeg.IsAvailable() ? _ffmpeg.ResolvedPath : Loc.Get("about.ffmpeg_missing");
            var avalonia = typeof(Application).Assembly.GetName().Version?.ToString(3);
            var builder = new StringBuilder();
            builder.AppendLine($"{AppName} {_localVersion}");
            builder.AppendLine(Loc.Get("about.debug_os", $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}"));
            builder.AppendLine(Loc.Get("about.debug_net", RuntimeInformation.FrameworkDescription));
            builder.AppendLine(Loc.Get("about.debug_avalonia", avalonia ?? string.Empty));
            builder.Append(Loc.Get("about.debug_ffmpeg", ffmpeg));
            return builder.ToString();
        }

        private string BuildRuntimeSummary()
        {
            var ffmpeg = _ffmpeg.IsAvailable() ? _ffmpeg.ResolvedPath : Loc.Get("about.ffmpeg_missing");
            var avalonia = typeof(Application).Assembly.GetName().Version?.ToString(3) ?? string.Empty;
            return Loc.Get(
                "about.runtime_summary",
                $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}",
                RuntimeInformation.FrameworkDescription,
                avalonia,
                ffmpeg);
        }

        private string FormatVersion(Version version)
        {
#if DEBUG
            return Loc.Get("about.version_debug", version);
#else
            return Loc.Get("about.version", version);
#endif
        }

        private string ReadLicense(Assembly assembly)
        {
            using var stream = assembly.GetManifestResourceStream(LICENSE_RESOURCE_NAME);
            if (stream is null)
                return Loc.Get("about.mit_license");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        }

        private static string NonEmpty(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value) || value.StartsWith("about.", StringComparison.Ordinal))
                return fallback;

            return value;
        }
    }
}
