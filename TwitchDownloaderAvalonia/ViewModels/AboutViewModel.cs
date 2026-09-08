using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class AboutViewModel : ViewModelBase
    {
        private const string LICENSE_RESOURCE_NAME = "TwitchDownloaderAvalonia.LICENSE.txt";
        private const string DEFAULT_CHANGELOG_URL = UpdateCheckService.DEFAULT_CHANGELOG_URL;

        private readonly UpdateCheckService _updates;
        private readonly DialogService _dialogs;
        private readonly FfmpegService _ffmpeg;
        private readonly Version _localVersion;
        private Task? _checkTask;

        public AboutViewModel(UpdateCheckService updates, DialogService dialogs, FfmpegService ffmpeg)
        {
            _updates = updates;
            _dialogs = dialogs;
            _ffmpeg = ffmpeg;

            var assembly = typeof(AboutViewModel).Assembly;
            _localVersion = assembly.GetName().Version?.StripRevisionIfDefault() ?? new Version(0, 0, 0);
            AppName = "Twitch Downloader";
            VersionText = FormatVersion(_localVersion);
            Description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "Download and render Twitch VODs, clips, and chats";
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright © lay295 and contributors";
            LicenseText = ReadLicense(assembly);
            ChangelogUrl = DEFAULT_CHANGELOG_URL;
            RuntimeSummary = BuildRuntimeSummary();
        }

        public string AppName { get; }
        public string VersionText { get; }
        public string Description { get; }
        public string Copyright { get; }
        public string LicenseText { get; }
        public string FrontendNote { get; } = "Avalonia UI for Windows, Linux, and macOS.";

        [ObservableProperty]
        public partial string RuntimeSummary { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateStatus))]
        public partial string? UpdateStatusText { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateStatus))]
        public partial bool IsCheckingUpdate { get; set; }

        [ObservableProperty]
        public partial bool HasUpdate { get; set; }

        [ObservableProperty]
        public partial string ChangelogUrl { get; set; }

        public bool ShowUpdateStatus => IsCheckingUpdate || !string.IsNullOrEmpty(UpdateStatusText);

        public Task EnsureUpdateCheckAsync()
        {
            RuntimeSummary = BuildRuntimeSummary();
            return _checkTask ??= CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            IsCheckingUpdate = true;
            UpdateStatusText = "Checking…";
            HasUpdate = false;

            try
            {
                var result = await _updates.CheckAsync(_localVersion);
                if (result is null)
                {
                    UpdateStatusText = null;
                    return;
                }

                ChangelogUrl = result.ChangelogUrl;
                if (result.IsNewer)
                {
                    HasUpdate = true;
                    UpdateStatusText = $"Version {result.RemoteVersion} is available";
                    return;
                }

                UpdateStatusText = "You're up to date";
            }
            catch
            {
                UpdateStatusText = null;
                HasUpdate = false;
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

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
            var ffmpeg = _ffmpeg.IsAvailable() ? _ffmpeg.ResolvedPath : "missing";
            var builder = new StringBuilder();
            builder.AppendLine($"{AppName} {_localVersion}");
            builder.AppendLine($"OS: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
            builder.AppendLine($"NET: {RuntimeInformation.FrameworkDescription}");
            builder.AppendLine($"Avalonia: {typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(3)}");
            builder.Append($"FFmpeg: {ffmpeg}");
            return builder.ToString();
        }

        private string BuildRuntimeSummary()
        {
            var ffmpeg = _ffmpeg.IsAvailable() ? _ffmpeg.ResolvedPath : "missing";
            var avalonia = typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(3);
            return $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture} · {RuntimeInformation.FrameworkDescription} · Avalonia {avalonia} · FFmpeg {ffmpeg}";
        }

        private static string FormatVersion(Version version)
        {
#if DEBUG
            return $"v{version} DEBUG";
#else
            return $"v{version}";
#endif
        }

        private static string ReadLicense(Assembly assembly)
        {
            using var stream = assembly.GetManifestResourceStream(LICENSE_RESOURCE_NAME);
            if (stream is null)
                return "MIT License";

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        }
    }
}
