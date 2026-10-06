using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _status;
        private readonly IFileDialogService _files;
        private readonly IDialogService _dialogs;
        private readonly FileCollisionService _collision;
        private readonly ThemeService _themes;
        private readonly UpdatePreferencesStore _updatePreferences;
        private static readonly StringComparer ThemeNames = StringComparer.OrdinalIgnoreCase;
        private bool _suppressSave;

        public SettingsViewModel(
            LocalizationService loc,
            ThemeService themes,
            SettingsService settings,
            AppStatus status,
            IFileDialogService files,
            IDialogService dialogs,
            FileCollisionService collision,
            QueueService queue,
            UpdatePreferencesStore updatePreferences) : base(loc)
        {
            _themes = themes;
            _settings = settings;
            _status = status;
            _files = files;
            _dialogs = dialogs;
            _collision = collision;
            Queue = queue;
            _updatePreferences = updatePreferences;
            LoadFromSettings();
        }

        public QueueService Queue { get; }
        public IReadOnlyList<CultureOption> Cultures => AvailableCultures.All;
        public string TempPathPlaceholder { get; } = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        public IReadOnlyList<ThemePickerItem> ThemePickerItems { get; private set; } = [];
        public IReadOnlyList<LabeledOption> CollisionOptions { get; private set; } = [];
        public IReadOnlyList<LabeledOption> QualityOptions { get; private set; } = [];
        public IReadOnlyList<FilenameParameter> FilenameParameters { get; private set; } = [];

        [ObservableProperty]
        public partial CultureOption SelectedCulture { get; set; } = AvailableCultures.English;

        [ObservableProperty]
        public partial ThemePickerItem? SelectedThemePickerItem { get; set; }

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
        public partial bool OfferUpdatesOnStartup { get; set; }

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
        public partial string SelectedQuality { get; set; } = QualityNames.SOURCE;

        public bool ShowDonateButton => !HideDonation;

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            _suppressSave = true;
            var collision = SelectedCollisionOption?.Value;
            var quality = SelectedQualityOption?.Value;

            RebuildLocalizedOptions();

            SelectedThemePickerItem = FindPickerItemFromSettings();
            SelectedCollisionOption = FindOption(CollisionOptions, collision, "Ask");
            SelectedQualityOption = FindOption(QualityOptions, quality, EnqueueOptionsViewModel.Qualities[0]);

            _suppressSave = false;
        }

        public void LoadFromSettings()
        {
            _suppressSave = true;
            var current = _settings.Current;
            var ui = current.Ui;
            var general = current.General;
            var queue = current.Queue;
            RebuildLocalizedOptions();
            SelectedCulture = AvailableCultures.FromCode(ui.Culture);
            SelectedThemePickerItem = FindPickerItemFromSettings();
            HideDonation = ui.HideDonation;
            ReduceMotion = ui.ReduceMotion;
            OfferUpdatesOnStartup = _updatePreferences.Load().OfferOnStartup;
            UtcVideoTime = general.UtcVideoTime;
            OAuth = general.OAuth;
            TempPath = general.TempPath;
            DownloadThrottleEnabled = general.DownloadThrottleEnabled;
            MaximumBandwidthKib = Math.Clamp(general.MaximumBandwidthKib, 1, 122070);
            SelectedCollision = CollisionToLabel(general.FileCollisionBehavior);
            SelectedCollisionOption = FindOption(CollisionOptions, SelectedCollision, "Ask");
            VerboseErrors = general.VerboseErrors;
            var levels = (LogLevel)ui.LogLevels;
            LogVerbose = levels.HasFlag(LogLevel.Verbose);
            LogInfo = levels.HasFlag(LogLevel.Info);
            LogWarning = levels.HasFlag(LogLevel.Warning);
            LogError = levels.HasFlag(LogLevel.Error);
            LogFfmpeg = levels.HasFlag(LogLevel.Ffmpeg);
            TemplateVod = general.TemplateVod;
            TemplateClip = general.TemplateClip;
            TemplateChat = general.TemplateChat;
            QueueFolder = queue.Folder;
            SelectedQuality = !EnqueueOptionsViewModel.Qualities.Contains(queue.PreferredQuality)
                ? EnqueueOptionsViewModel.Qualities[0]
                : queue.PreferredQuality;

            SelectedQualityOption = FindOption(QualityOptions, SelectedQuality, EnqueueOptionsViewModel.Qualities[0]);
            _suppressSave = false;
        }

        private void RebuildLocalizedOptions()
        {
            ThemePickerItems = BuildThemePickerItems();
            CollisionOptions =
            [
                new LabeledOption("Ask", Loc.Get("settings.collision_ask")),
                new LabeledOption("Overwrite", Loc.Get("settings.collision_overwrite")),
                new LabeledOption("Rename", Loc.Get("settings.collision_rename")),
                new LabeledOption("Cancel", Loc.Get("settings.collision_cancel")),
            ];

            QualityOptions = [.. EnqueueOptionsViewModel.Qualities.Select(value => new LabeledOption(value, QualityLabels.Get(Loc, value)))];
            FilenameParameters =
            [
                new FilenameParameter(Loc, "{title}", "settings.param_title"),
                new FilenameParameter(Loc, "{id}", "settings.param_id"),
                new FilenameParameter(Loc, "{date}", "settings.param_date"),
                new FilenameParameter(Loc, "{date_custom=\"\"}", "settings.param_date_custom"),
                new FilenameParameter(Loc, "{channel}", "settings.param_channel"),
                new FilenameParameter(Loc, "{channel_id}", "settings.param_channel_id"),
                new FilenameParameter(Loc, "{clipper}", "settings.param_clipper"),
                new FilenameParameter(Loc, "{clipper_id}", "settings.param_clipper_id"),
                new FilenameParameter(Loc, "{random_string}", "settings.param_random"),
                new FilenameParameter(Loc, "{trim_start}", "settings.param_trim_start"),
                new FilenameParameter(Loc, "{trim_start_custom=\"\"}", "settings.param_trim_start_custom"),
                new FilenameParameter(Loc, "{trim_end}", "settings.param_trim_end"),
                new FilenameParameter(Loc, "{trim_end_custom=\"\"}", "settings.param_trim_end_custom"),
                new FilenameParameter(Loc, "{trim_length}", "settings.param_trim_length"),
                new FilenameParameter(Loc, "{trim_length_custom=\"\"}", "settings.param_trim_length_custom"),
                new FilenameParameter(Loc, "{length}", "settings.param_length"),
                new FilenameParameter(Loc, "{length_custom=\"\"}", "settings.param_length_custom"),
                new FilenameParameter(Loc, "{views}", "settings.param_views"),
                new FilenameParameter(Loc, "{game}", "settings.param_game"),
            ];

            OnPropertyChanged(nameof(ThemePickerItems));
            OnPropertyChanged(nameof(CollisionOptions));
            OnPropertyChanged(nameof(QualityOptions));
            OnPropertyChanged(nameof(FilenameParameters));
        }

        public void RefreshThemeOptions()
        {
            _suppressSave = true;
            RebuildLocalizedOptions();
            SelectedThemePickerItem = FindPickerItemFromSettings();
            _suppressSave = false;
        }

        private List<ThemePickerItem> BuildThemePickerItems()
        {
            var current = _settings.Current;
            var items = new List<ThemePickerItem>
            {
                new(ThemeService.SYSTEM, Loc.Get("settings.theme_system"), IsSystem: true),
                new(string.Empty, Loc.Get("settings.theme_light_themes"), IsHeader: true),
            };

            var options = _themes.GetThemeOptions();
            foreach (var name in options.Light)
            {
                items.Add(new ThemePickerItem(name, ThemeNames.Equals(name, ThemeService.LIGHT) ? Loc.Get("settings.theme_light_default") : name,
                    IsPreferred: ThemeNames.Equals(name, current.Ui.LightTheme)));
            }

            items.Add(new ThemePickerItem(string.Empty, Loc.Get("settings.theme_dark_themes"), IsHeader: true));
            foreach (var name in options.Dark)
            {
                items.Add(new ThemePickerItem(name, ThemeNames.Equals(name, ThemeService.DARK) ? Loc.Get("settings.theme_dark_default") : name,
                    IsDark: true,
                    IsPreferred: ThemeNames.Equals(name, current.Ui.DarkTheme)));
            }

            return items;
        }

        private ThemePickerItem FindPickerItemFromSettings()
        {
            var current = _settings.Current;
            if (ThemePickerItems.Count == 0 || ThemeNames.Equals(current.Ui.Theme, ThemeService.SYSTEM))
                return ThemePickerItems.FirstOrDefault(static item => item.IsSystem) ?? ThemePickerItems[0];

            var isDark = ThemeNames.Equals(current.Ui.Theme, ThemeService.DARK);
            var preferred = isDark
                ? current.Ui.DarkTheme
                : current.Ui.LightTheme;

            var builtin = isDark
                ? ThemeService.DARK
                : ThemeService.LIGHT;

            return FindNamedPack(isDark, preferred) ?? FindNamedPack(isDark, builtin) ?? ThemePickerItems[0];
        }

        private ThemePickerItem? FindNamedPack(bool isDark, string name) =>
            ThemePickerItems.FirstOrDefault(item => item.IsSelectable && item.IsDark == isDark && item.HasName(name));

        private static LabeledOption FindOption(
            IReadOnlyList<LabeledOption> options,
            string? value,
            string fallback,
            StringComparison comparison = StringComparison.Ordinal)
        {
            return options.FirstOrDefault(option => option.Value.Equals(value, comparison))
                ?? options.FirstOrDefault(option => option.Value.Equals(fallback, comparison))
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

            CacheDirectoryService.ClearCacheDirectory(_settings.Current.General.TempPath, out var selectedError);
            CacheDirectoryService.ClearCacheDirectory(Path.GetTempPath(), out var defaultError);
            var error = selectedError ?? defaultError;
            if (error is not null)
                await _dialogs.ShowErrorAsync(Loc.Get("settings.clear_cache_title"), error.Message);
        }

        [RelayCommand]
        private async Task ExportSettingsAsync()
        {
            var path = await _files.SaveFileAsync(
                "twitchdownloader-settings.json",
                Loc.Get("settings.export_filter"),
                "json");
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                _settings.ExportTo(path);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync(Loc.Get("settings.export"), ex.Message);
            }
        }

        [RelayCommand]
        private async Task ImportSettingsAsync()
        {
            var path = await _files.OpenFileAsync(Loc.Get("settings.import"), Loc.Get("settings.export_filter"), ["*.json"]);
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!_settings.TryImportFrom(path))
            {
                await _dialogs.ShowErrorAsync(Loc.Get("settings.import_failed"), Loc.Get("settings.import_failed_message"));
                return;
            }

            _collision.ResetSessionBehavior();
            LoadFromSettings();
            Loc.SetCulture(SelectedCulture.Code);
            _themes.Apply(_settings.Current);
            _status.ReduceMotion = ReduceMotion;
            Queue.NotifyLimitsChanged();
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
            _updatePreferences.Save(UpdatePromptPolicy.WithOfferOnStartup(_updatePreferences.Load(), true));
            LoadFromSettings();
            Loc.SetCulture(SelectedCulture.Code);
            _themes.Apply(_settings.Current);
            _status.ReduceMotion = ReduceMotion;
            Queue.NotifyLimitsChanged();
        }

        [RelayCommand]
        private void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        partial void OnSelectedCultureChanged(CultureOption value)
        {
            if (_suppressSave)
                return;

            _settings.Current.Ui.Culture = value.Code;
            _settings.Save();
            Loc.SetCulture(value.Code);
        }

        partial void OnSelectedThemePickerItemChanged(ThemePickerItem? value)
        {
            if (_suppressSave || value is null)
                return;

            if (value.IsHeader)
            {
                _suppressSave = true;
                SelectedThemePickerItem = FindPickerItemFromSettings();
                _suppressSave = false;
                return;
            }

            if (value.IsSystem)
            {
                _settings.Current.Ui.Theme = ThemeService.SYSTEM;
            }
            else if (value.IsDark)
            {
                _settings.Current.Ui.Theme = ThemeService.DARK;
                _settings.Current.Ui.DarkTheme = value.Value;
            }
            else
            {
                _settings.Current.Ui.Theme = ThemeService.LIGHT;
                _settings.Current.Ui.LightTheme = value.Value;
            }

            _settings.Save();
            _themes.Apply(_settings.Current);

            _suppressSave = true;
            ThemePickerItems = BuildThemePickerItems();
            OnPropertyChanged(nameof(ThemePickerItems));
            SelectedThemePickerItem = FindPickerItemFromSettings();
            _suppressSave = false;
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

            _settings.Current.Ui.HideDonation = value;
            _settings.Save();
        }

        partial void OnReduceMotionChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.Ui.ReduceMotion = value;
            _settings.Save();
            _status.ReduceMotion = value;
        }

        partial void OnOfferUpdatesOnStartupChanged(bool value)
        {
            if (_suppressSave)
                return;

            var prefs = _updatePreferences.Load();
            _updatePreferences.Save(UpdatePromptPolicy.WithOfferOnStartup(prefs, value));
        }

        partial void OnUtcVideoTimeChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.UtcVideoTime = value;
            _settings.Save();
        }

        partial void OnOAuthChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.OAuth = value;
            _settings.Save();
        }

        partial void OnTempPathChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.TempPath = value;
            _settings.Save();
        }

        partial void OnDownloadThrottleEnabledChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.DownloadThrottleEnabled = value;
            _settings.Save();
        }

        partial void OnMaximumBandwidthKibChanged(int value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.MaximumBandwidthKib = Math.Clamp(value, 1, 122070);
            _settings.Save();
        }

        partial void OnSelectedCollisionChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.FileCollisionBehavior = LabelToCollision(value);
            _settings.Save();
            _collision.ResetSessionBehavior();
        }

        partial void OnVerboseErrorsChanged(bool value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.VerboseErrors = value;
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

            _settings.Current.General.TemplateVod = value;
            _settings.Save();
        }

        partial void OnTemplateClipChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.TemplateClip = value;
            _settings.Save();
        }

        partial void OnTemplateChatChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.General.TemplateChat = value;
            _settings.Save();
        }

        partial void OnQueueFolderChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.Queue.Folder = value;
            _settings.Save();
        }

        partial void OnSelectedQualityChanged(string value)
        {
            if (_suppressSave)
                return;

            _settings.Current.Queue.PreferredQuality = value;
            _settings.Save();
        }

        private void SetLogFlag(LogLevel flag, bool enabled)
        {
            if (_suppressSave)
                return;

            var levels = (LogLevel)_settings.Current.Ui.LogLevels;
            levels = enabled ? levels | flag : levels & ~flag;
            _settings.Current.Ui.LogLevels = (int)levels;
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
