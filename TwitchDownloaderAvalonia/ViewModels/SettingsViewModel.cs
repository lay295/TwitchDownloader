using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Converters;
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
        public IReadOnlyList<CultureOption> Cultures => AvailableCultures.All;
        public string TempPathPlaceholder { get; } = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public IReadOnlyList<LabeledOption> ThemeOptions { get; private set; } = [];
        public IReadOnlyList<LabeledOption> CollisionOptions { get; private set; } = [];
        public IReadOnlyList<LabeledOption> QualityOptions { get; private set; } = [];
        public IReadOnlyList<FilenameParameter> FilenameParameters { get; private set; } = [];

        [ObservableProperty]
        public partial CultureOption SelectedCulture { get; set; } = AvailableCultures.English;

        [ObservableProperty]
        public partial LabeledOption? SelectedThemeOption { get; set; }

        [ObservableProperty]
        public partial LabeledOption? SelectedCollisionOption { get; set; }

        [ObservableProperty]
        public partial LabeledOption? SelectedQualityOption { get; set; }

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

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            _suppressSave = true;
            var theme = SelectedThemeOption?.Value;
            var collision = SelectedCollisionOption?.Value;
            var quality = SelectedQualityOption?.Value;
            RebuildLocalizedOptions();
            SelectedThemeOption = FindOption(ThemeOptions, theme, ThemeService.System);
            SelectedCollisionOption = FindOption(CollisionOptions, collision, "Ask");
            SelectedQualityOption = FindOption(QualityOptions, quality, EnqueueOptionsViewModel.Qualities[0]);
            _suppressSave = false;
        }

        public void LoadFromSettings()
        {
            _suppressSave = true;
            var current = _settings.Current;
            RebuildLocalizedOptions();
            SelectedCulture = AvailableCultures.FromCode(current.GuiCulture);
            SelectedThemeOption = FindOption(ThemeOptions, current.GuiTheme, ThemeService.System);
            HideDonation = current.HideDonation;
            ReduceMotion = current.ReduceMotion;
            UtcVideoTime = current.UtcVideoTime;
            OAuth = current.OAuth;
            TempPath = current.TempPath;
            DownloadThrottleEnabled = current.DownloadThrottleEnabled;
            MaximumBandwidthKib = Math.Clamp(current.MaximumBandwidthKib, 1, 122070);
            SelectedCollision = CollisionToLabel(current.FileCollisionBehavior);
            SelectedCollisionOption = FindOption(CollisionOptions, SelectedCollision, "Ask");
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

            SelectedQualityOption = FindOption(QualityOptions, SelectedQuality, EnqueueOptionsViewModel.Qualities[0]);
            _suppressSave = false;
        }

        private void RebuildLocalizedOptions()
        {
            ThemeOptions =
            [
                new LabeledOption(ThemeService.System, Loc.Get("settings.theme_system")),
                new LabeledOption(ThemeService.Light, Loc.Get("settings.theme_light")),
                new LabeledOption(ThemeService.Dark, Loc.Get("settings.theme_dark")),
            ];
            CollisionOptions =
            [
                new LabeledOption("Ask", Loc.Get("settings.collision_ask")),
                new LabeledOption("Overwrite", Loc.Get("settings.collision_overwrite")),
                new LabeledOption("Rename", Loc.Get("settings.collision_rename")),
                new LabeledOption("Cancel", Loc.Get("settings.collision_cancel")),
            ];
            QualityOptions = [.. EnqueueOptionsViewModel.Qualities.Select(value => new LabeledOption(value, QualityLabels.Get(value)))];
            FilenameParameters =
            [
                new FilenameParameter("{title}", "settings.param_title"),
                new FilenameParameter("{id}", "settings.param_id"),
                new FilenameParameter("{date}", "settings.param_date"),
                new FilenameParameter("{date_custom=\"\"}", "settings.param_date_custom"),
                new FilenameParameter("{channel}", "settings.param_channel"),
                new FilenameParameter("{channel_id}", "settings.param_channel_id"),
                new FilenameParameter("{clipper}", "settings.param_clipper"),
                new FilenameParameter("{clipper_id}", "settings.param_clipper_id"),
                new FilenameParameter("{random_string}", "settings.param_random"),
                new FilenameParameter("{trim_start}", "settings.param_trim_start"),
                new FilenameParameter("{trim_start_custom=\"\"}", "settings.param_trim_start_custom"),
                new FilenameParameter("{trim_end}", "settings.param_trim_end"),
                new FilenameParameter("{trim_end_custom=\"\"}", "settings.param_trim_end_custom"),
                new FilenameParameter("{trim_length}", "settings.param_trim_length"),
                new FilenameParameter("{trim_length_custom=\"\"}", "settings.param_trim_length_custom"),
                new FilenameParameter("{length}", "settings.param_length"),
                new FilenameParameter("{length_custom=\"\"}", "settings.param_length_custom"),
                new FilenameParameter("{views}", "settings.param_views"),
                new FilenameParameter("{game}", "settings.param_game"),
            ];
            OnPropertyChanged(nameof(ThemeOptions));
            OnPropertyChanged(nameof(CollisionOptions));
            OnPropertyChanged(nameof(QualityOptions));
            OnPropertyChanged(nameof(FilenameParameters));
        }

        private static LabeledOption FindOption(IReadOnlyList<LabeledOption> options, string? value, string fallback)
        {
            return options.FirstOrDefault(option => option.Value == value)
                ?? options.FirstOrDefault(option => option.Value == fallback)
                ?? options[0];
        }

        [RelayCommand]
        private async Task BrowseTempPathAsync()
        {
            var start = string.IsNullOrWhiteSpace(TempPath) ? TempPathPlaceholder : TempPath;
            var path = await _files.PickFolderAsync(Loc.Get("settings.cache_folder"), start);
            if (!string.IsNullOrWhiteSpace(path))
                TempPath = path;
        }

        [RelayCommand]
        private async Task BrowseQueueFolderAsync()
        {
            var path = await _files.PickFolderAsync(Loc.Get("settings.download_folder"), QueueFolder);
            if (!string.IsNullOrWhiteSpace(path))
                QueueFolder = path;
        }

        [RelayCommand]
        private async Task ClearCacheAsync()
        {
            var confirmed = await _dialogs.ShowConfirmAsync(
                Loc.Get("settings.clear_cache_title"),
                Loc.Get("settings.clear_cache_confirm"));

            if (!confirmed)
                return;

            CacheDirectoryService.ClearCacheDirectory(_settings.Current.TempPath, out var selectedError);
            CacheDirectoryService.ClearCacheDirectory(Path.GetTempPath(), out var defaultError);
            var error = selectedError ?? defaultError;
            if (error is not null)
                await _dialogs.ShowErrorAsync(Loc.Get("settings.clear_cache_title"), error.Message);
        }

        [RelayCommand]
        private async Task RestoreDefaultsAsync()
        {
            var confirmed = await _dialogs.ShowConfirmAsync(
                Loc.Get("settings.restore_title"),
                Loc.Get("settings.restore_confirm"));

            if (!confirmed)
                return;

            _settings.ResetToDefaults();
            _collision.ResetSessionBehavior();
            LoadFromSettings();
            LocalizationService.Current.SetCulture(SelectedCulture.Code);
            ThemeService.Apply(SelectedThemeOption?.Value ?? ThemeService.System);
            _status.ReduceMotion = ReduceMotion;
            Queue.NotifyLimitsChanged();
        }

        [RelayCommand]
        private void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        partial void OnSelectedCultureChanged(CultureOption value)
        {
            if (_suppressSave)
                return;

            _settings.Current.GuiCulture = value.Code;
            _settings.Save();
            LocalizationService.Current.SetCulture(value.Code);
        }

        partial void OnSelectedThemeOptionChanged(LabeledOption? value)
        {
            if (_suppressSave || value is null)
                return;

            _settings.Current.GuiTheme = value.Value;
            _settings.Save();
            ThemeService.Apply(value.Value);
        }

        partial void OnSelectedCollisionOptionChanged(LabeledOption? value)
        {
            if (_suppressSave || value is null)
                return;

            SelectedCollision = value.Value;
        }

        partial void OnSelectedQualityOptionChanged(LabeledOption? value)
        {
            if (_suppressSave || value is null)
                return;

            SelectedQuality = value.Value;
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
