using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _status;
        private readonly FileDialogService _files;
        private readonly DialogService _dialogs;
        private readonly FileCollisionService _collision;
        private bool _suppressSave;

        public SettingsViewModel(
            SettingsService settings,
            AppStatus status,
            FileDialogService files,
            DialogService dialogs,
            FileCollisionService collision,
            QueueService queue)
        {
            _settings = settings;
            _status = status;
            _files = files;
            _dialogs = dialogs;
            _collision = collision;
            Queue = queue;
            LoadFromSettings();
        }

        public QueueService Queue { get; }
        public IReadOnlyList<string> Themes => ThemeService.Options;
        public IReadOnlyList<string> Qualities => EnqueueOptionsViewModel.Qualities;
        public IReadOnlyList<string> CollisionLabels { get; } = ["Ask", "Overwrite", "Rename", "Cancel"];
        public string TempPathPlaceholder { get; } = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public IReadOnlyList<FilenameParameter> FilenameParameters { get; } =
        [
            new("{title}", "The title of the video/clip."),
            new("{id}", "The ID of the video/clip."),
            new("{date}", "The date that the video/clip was created in the format M-d-yy."),
            new("{date_custom=\"\"}", "The date that the video/clip was created in a customizable format."),
            new("{channel}", "The display name of the channel which owns the video/clip/chat."),
            new("{channel_id}", "The ID of the channel which owns the video/clip/chat."),
            new("{clipper}", "The display name of the channel which created the clip, or empty for videos."),
            new("{clipper_id}", "The ID of the channel which created the clip, or empty for videos."),
            new("{random_string}", "A string of 11 random characters."),
            new("{trim_start}", "The start trim of the video/chat in the format hh-mm-ss."),
            new("{trim_start_custom=\"\"}", "The start trim of the video/chat in a customizable format."),
            new("{trim_end}", "The end trim of the video/chat in the format hh-mm-ss."),
            new("{trim_end_custom=\"\"}", "The end trim of the video/chat in a customizable format."),
            new("{trim_length}", "The length (including trim) of the video/clip/chat in the format hh-mm-ss."),
            new("{trim_length_custom=\"\"}", "The length (including trim) of the video/clip/chat in a customizable format."),
            new("{length}", "The length (excluding trim) of the video/clip/chat in the format hh-mm-ss."),
            new("{length_custom=\"\"}", "The length (excluding trim) of the video/clip/chat in a customizable format."),
            new("{views}", "The amount of views the video/clip has."),
            new("{game}", "The display name of the primary game/category in the video/clip/chat."),
        ];

        [ObservableProperty]
        public partial string SelectedTheme { get; set; } = ThemeService.System;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowDonateButton))]
        public partial bool HideDonation { get; set; }

        [ObservableProperty]
        public partial bool ReduceMotion { get; set; }

        [ObservableProperty]
        public partial bool UtcVideoTime { get; set; }

        [ObservableProperty]
        public partial string OAuth { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TempPath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool DownloadThrottleEnabled { get; set; }

        [ObservableProperty]
        public partial int MaximumBandwidthKib { get; set; }

        [ObservableProperty]
        public partial string SelectedCollision { get; set; } = "Ask";

        [ObservableProperty]
        public partial bool VerboseErrors { get; set; }

        [ObservableProperty]
        public partial bool LogVerbose { get; set; }

        [ObservableProperty]
        public partial bool LogInfo { get; set; }

        [ObservableProperty]
        public partial bool LogWarning { get; set; }

        [ObservableProperty]
        public partial bool LogError { get; set; }

        [ObservableProperty]
        public partial bool LogFfmpeg { get; set; }

        [ObservableProperty]
        public partial string TemplateVod { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TemplateClip { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string TemplateChat { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string QueueFolder { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedQuality { get; set; } = "Source";

        public bool ShowDonateButton => !HideDonation;

        public void LoadFromSettings()
        {
            _suppressSave = true;
            var current = _settings.Current;
            SelectedTheme = !ThemeService.Options.Contains(current.GuiTheme)
                ? ThemeService.System
                : current.GuiTheme;

            HideDonation = current.HideDonation;
            ReduceMotion = current.ReduceMotion;
            UtcVideoTime = current.UtcVideoTime;
            OAuth = current.OAuth;
            TempPath = current.TempPath;
            DownloadThrottleEnabled = current.DownloadThrottleEnabled;
            MaximumBandwidthKib = Math.Clamp(current.MaximumBandwidthKib, 1, 122070);
            SelectedCollision = CollisionToLabel(current.FileCollisionBehavior);
            VerboseErrors = current.VerboseErrors;
            var levels = (LogLevel)current.LogLevels;
            LogVerbose = levels.HasFlag(LogLevel.Verbose);
            LogInfo = levels.HasFlag(LogLevel.Info);
            LogWarning = levels.HasFlag(LogLevel.Warning);
            LogError = levels.HasFlag(LogLevel.Error);
            LogFfmpeg = levels.HasFlag(LogLevel.Ffmpeg);
            TemplateVod = current.TemplateVod;
            TemplateClip = current.TemplateClip;
            TemplateChat = current.TemplateChat;
            QueueFolder = current.QueueFolder;
            SelectedQuality = !EnqueueOptionsViewModel.Qualities.Contains(current.PreferredQuality)
                ? EnqueueOptionsViewModel.Qualities[0]
                : current.PreferredQuality;

            _suppressSave = false;
        }

        [RelayCommand]
        private async Task BrowseTempPathAsync()
        {
            var start = string.IsNullOrWhiteSpace(TempPath) ? TempPathPlaceholder : TempPath;
            var path = await _files.PickFolderAsync("Cache folder", start);
            if (!string.IsNullOrWhiteSpace(path))
                TempPath = path;
        }

        [RelayCommand]
        private async Task BrowseQueueFolderAsync()
        {
            var path = await _files.PickFolderAsync("Download folder", QueueFolder);
            if (!string.IsNullOrWhiteSpace(path))
                QueueFolder = path;
        }

        [RelayCommand]
        private async Task ClearCacheAsync()
        {
            var confirmed = await _dialogs.ShowConfirmAsync(
                "Clear cache",
                "Are you sure you want to clear your cache?\nYou should only really do this if the program isn't working correctly.");
            if (!confirmed)
                return;

            CacheDirectoryService.ClearCacheDirectory(_settings.Current.TempPath, out var selectedError);
            CacheDirectoryService.ClearCacheDirectory(Path.GetTempPath(), out var defaultError);
            var error = selectedError ?? defaultError;
            if (error is not null)
                await _dialogs.ShowErrorAsync("Clear cache", error.Message);
        }

        [RelayCommand]
        private async Task RestoreDefaultsAsync()
        {
            var confirmed = await _dialogs.ShowConfirmAsync(
                "Restore defaults",
                "Are you sure you want to restore all settings to their default values?");
            if (!confirmed)
                return;

            _settings.ResetToDefaults();
            _collision.ResetSessionBehavior();
            LoadFromSettings();
            ThemeService.Apply(SelectedTheme);
            _status.ReduceMotion = ReduceMotion;
            Queue.NotifyLimitsChanged();
        }

        [RelayCommand]
        private void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        partial void OnSelectedThemeChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.GuiTheme = value;
            _settings.Save();
            ThemeService.Apply(value);
        }

        partial void OnHideDonationChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.HideDonation = value;
            _settings.Save();
        }

        partial void OnReduceMotionChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.ReduceMotion = value;
            _settings.Save();
            _status.ReduceMotion = value;
        }

        partial void OnUtcVideoTimeChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.UtcVideoTime = value;
            _settings.Save();
        }

        partial void OnOAuthChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.OAuth = value;
            _settings.Save();
        }

        partial void OnTempPathChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.TempPath = value;
            _settings.Save();
        }

        partial void OnDownloadThrottleEnabledChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.DownloadThrottleEnabled = value;
            _settings.Save();
        }

        partial void OnMaximumBandwidthKibChanged(int value)
        {
            if (_suppressSave)
                return;

            _settings.Current.MaximumBandwidthKib = Math.Clamp(value, 1, 122070);
            _settings.Save();
        }

        partial void OnSelectedCollisionChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.FileCollisionBehavior = LabelToCollision(value);
            _settings.Save();
            _collision.ResetSessionBehavior();
        }

        partial void OnVerboseErrorsChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.VerboseErrors = value;
            _settings.Save();
        }

        partial void OnLogVerboseChanged(bool value) => SetLogFlag(LogLevel.Verbose, value);
        partial void OnLogInfoChanged(bool value) => SetLogFlag(LogLevel.Info, value);
        partial void OnLogWarningChanged(bool value) => SetLogFlag(LogLevel.Warning, value);
        partial void OnLogErrorChanged(bool value) => SetLogFlag(LogLevel.Error, value);
        partial void OnLogFfmpegChanged(bool value) => SetLogFlag(LogLevel.Ffmpeg, value);

        partial void OnTemplateVodChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.TemplateVod = value;
            _settings.Save();
        }

        partial void OnTemplateClipChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.TemplateClip = value;
            _settings.Save();
        }

        partial void OnTemplateChatChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.TemplateChat = value;
            _settings.Save();
        }

        partial void OnQueueFolderChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.QueueFolder = value;
            _settings.Save();
        }

        partial void OnSelectedQualityChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.PreferredQuality = value;
            _settings.Save();
        }

        private void SetLogFlag(LogLevel flag, bool enabled)
        {
            if (_suppressSave)
                return;

            var levels = (LogLevel)_settings.Current.LogLevels;
            levels = enabled ? levels | flag : levels & ~flag;
            _settings.Current.LogLevels = (int)levels;
            _settings.Save();
        }

        private static string CollisionToLabel(CollisionBehavior behavior) => behavior switch
        {
            CollisionBehavior.Overwrite => "Overwrite",
            CollisionBehavior.Rename => "Rename",
            CollisionBehavior.Cancel => "Cancel",
            _ => "Ask",
        };

        private static CollisionBehavior LabelToCollision(string label) => label switch
        {
            "Overwrite" => CollisionBehavior.Overwrite,
            "Rename" => CollisionBehavior.Rename,
            "Cancel" => CollisionBehavior.Cancel,
            _ => CollisionBehavior.Prompt,
        };
    }
}
