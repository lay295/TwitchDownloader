using System.Text.Json;
using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class ChatRenderViewModel : ViewModelBase
    {
        private static readonly HashSet<string> SettingProperties =
        [
            nameof(SelectedFont),
            nameof(FontSize),
            nameof(ChatWidth),
            nameof(ChatHeight),
            nameof(FontColorHex),
            nameof(BackgroundColorHex),
            nameof(AlternateBackgroundColorHex),
            nameof(HighlightUsersColorHex),
            nameof(Outline),
            nameof(Timestamp),
            nameof(SubMessages),
            nameof(ChatBadges),
            nameof(UpdateRate),
            nameof(DisperseCommentOffsets),
            nameof(AlternateMessageBackgrounds),
            nameof(AdjustUsernameVisibility),
            nameof(BttvEmotes),
            nameof(FfzEmotes),
            nameof(StvEmotes),
            nameof(RenderUserAvatars),
            nameof(Offline),
            nameof(IgnoreUsersList),
            nameof(BannedWordsList),
            nameof(HighlightUsersList),
            nameof(EmojiVendor),
            nameof(FilterBroadcaster),
            nameof(FilterModerator),
            nameof(FilterVip),
            nameof(FilterSubscriber),
            nameof(FilterPredictions),
            nameof(FilterNoAudioVisual),
            nameof(FilterPrimeGaming),
            nameof(FilterOther),
            nameof(EmoteScale),
            nameof(BadgeScale),
            nameof(EmojiScale),
            nameof(AvatarScale),
            nameof(OutlineScale),
            nameof(UsernameFontScale),
            nameof(VerticalSpacingScale),
            nameof(SectionHeightScale),
            nameof(WordSpacingScale),
            nameof(EmoteSpacingScale),
            nameof(AccentStrokeScale),
            nameof(AccentIndentScale),
            nameof(SidePaddingScale),
            nameof(Framerate),
            nameof(GenerateMask),
            nameof(Sharpening),
        ];

        private readonly SettingsService _settings;
        private readonly FfmpegService _ffmpeg;
        private readonly IDialogService _dialogs;
        private readonly IFileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private readonly QueueService _queue;

        private bool _loading;
        private bool _suppressPreset;

        private QueueItemViewModel? _queued;
        private ChatRoot? _chatJson;
        private string _videoId = "-1";
        private DateTime _videoTime;
        private TimeSpan _videoLength;
        private double _chatStartSeconds;
        private int _viewCount;

        private string _game = string.Empty;
        private string _streamerId = string.Empty;
        private string _streamerName = string.Empty;
        private string _clipperName = string.Empty;
        private string _clipperId = string.Empty;
        private string _title = string.Empty;

        private bool _loadingFfmpeg;
        private bool _hasCreatedAt;

        public ChatRenderViewModel(
            LocalizationService loc,
            SettingsService settings,
            AppStatus appStatus,
            FfmpegService ffmpeg,
            IDialogService dialogs,
            IFileDialogService fileDialogs,
            FileCollisionService collision,
            ThumbnailService thumbnails,
            QueueService queue) : base(loc)
        {
            _settings = settings;
            AppStatus = appStatus;
            _ffmpeg = ffmpeg;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _collision = collision;
            _thumbnails = thumbnails;
            _queue = queue;
            UrlBox = new UrlBoxActionsViewModel(loc, dialogs, value => InputFile = value);

            _settings.SettingsReloaded += OnSettingsReloaded;

            foreach (var font in LoadFonts())
                Fonts.Add(font);

            foreach (var container in RenderEncodingPresets.CreateContainers())
                Containers.Add(container);

            _loading = true;
            try
            {
                LoadFromSettings();
                LoadFfmpegArgs();
                RefreshPresets();
            }
            finally
            {
                _loading = false;
            }

            Status = Loc.Get("status.idle");
        }

        protected override void DisposeCore()
        {
            _settings.SettingsReloaded -= OnSettingsReloaded;
        }

        public UrlBoxActionsViewModel UrlBox { get; }

        public ObservableCollection<string> Fonts { get; } = [];
        public ObservableCollection<RenderContainer> Containers { get; } = [];
        public ObservableCollection<RenderCodec> Codecs { get; } = [];
        public ObservableCollection<string> PresetNames { get; } = [];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeletePresetCommand))]
        public partial string? SelectedPresetName { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SavePresetCommand))]
        public partial string PresetName { get; set; } = string.Empty;

        public bool CanSavePreset => !string.IsNullOrWhiteSpace(PresetName);
        public bool CanDeletePreset => !string.IsNullOrWhiteSpace(SelectedPresetName);

        [ObservableProperty]
        public partial string InputFile { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? SelectedFont { get; set; }

        [ObservableProperty]
        public partial double FontSize { get; set; } = 24;

        [ObservableProperty]
        public partial int ChatWidth { get; set; } = 700;

        [ObservableProperty]
        public partial int ChatHeight { get; set; } = 1200;

        [ObservableProperty]
        public partial string FontColorHex { get; set; } = "#FFFFFFFF";

        [ObservableProperty]
        public partial string BackgroundColorHex { get; set; } = "#FF111111";

        [ObservableProperty]
        public partial string AlternateBackgroundColorHex { get; set; } = "#FF191919";

        [ObservableProperty]
        public partial string HighlightUsersColorHex { get; set; } = "#C8FF0064";

        [ObservableProperty]
        public partial bool Outline { get; set; }

        [ObservableProperty]
        public partial bool Timestamp { get; set; }

        [ObservableProperty]
        public partial bool SubMessages { get; set; } = true;

        [ObservableProperty]
        public partial bool ChatBadges { get; set; } = true;

        [ObservableProperty]
        public partial double UpdateRate { get; set; } = 0.2;

        [ObservableProperty]
        public partial bool DisperseCommentOffsets { get; set; } = true;

        [ObservableProperty]
        public partial bool AlternateMessageBackgrounds { get; set; }

        [ObservableProperty]
        public partial bool AdjustUsernameVisibility { get; set; } = true;

        [ObservableProperty]
        public partial bool BttvEmotes { get; set; } = true;

        [ObservableProperty]
        public partial bool FfzEmotes { get; set; } = true;

        [ObservableProperty]
        public partial bool StvEmotes { get; set; } = true;

        [ObservableProperty]
        public partial bool RenderUserAvatars { get; set; }

        [ObservableProperty]
        public partial bool Offline { get; set; }

        [ObservableProperty]
        public partial string IgnoreUsersList { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string BannedWordsList { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string HighlightUsersList { get; set; } = string.Empty;

        [ObservableProperty]
        public partial EmojiVendor EmojiVendor { get; set; } = EmojiVendor.GoogleNotoColor;

        [ObservableProperty]
        public partial bool FilterBroadcaster { get; set; }

        [ObservableProperty]
        public partial bool FilterModerator { get; set; }

        [ObservableProperty]
        public partial bool FilterVip { get; set; }

        [ObservableProperty]
        public partial bool FilterSubscriber { get; set; }

        [ObservableProperty]
        public partial bool FilterPredictions { get; set; }

        [ObservableProperty]
        public partial bool FilterNoAudioVisual { get; set; }

        [ObservableProperty]
        public partial bool FilterPrimeGaming { get; set; }

        [ObservableProperty]
        public partial bool FilterOther { get; set; }

        [ObservableProperty]
        public partial double EmoteScale { get; set; } = 1;

        [ObservableProperty]
        public partial double BadgeScale { get; set; } = 1;

        [ObservableProperty]
        public partial double EmojiScale { get; set; } = 1;

        [ObservableProperty]
        public partial double AvatarScale { get; set; } = 1;

        [ObservableProperty]
        public partial double OutlineScale { get; set; } = 1;

        [ObservableProperty]
        public partial double UsernameFontScale { get; set; } = 1;

        [ObservableProperty]
        public partial double VerticalSpacingScale { get; set; } = 1;

        [ObservableProperty]
        public partial double SectionHeightScale { get; set; } = 1;

        [ObservableProperty]
        public partial double WordSpacingScale { get; set; } = 1;

        [ObservableProperty]
        public partial double EmoteSpacingScale { get; set; } = 1;

        [ObservableProperty]
        public partial double AccentStrokeScale { get; set; } = 1;

        [ObservableProperty]
        public partial double AccentIndentScale { get; set; } = 1;

        [ObservableProperty]
        public partial double SidePaddingScale { get; set; } = 1;

        [ObservableProperty]
        public partial RenderContainer? SelectedContainer { get; set; }

        [ObservableProperty]
        public partial RenderCodec? SelectedCodec { get; set; }

        [ObservableProperty]
        public partial int Framerate { get; set; } = 60;

        [ObservableProperty]
        public partial bool GenerateMask { get; set; }

        [ObservableProperty]
        public partial bool Sharpening { get; set; }

        [ObservableProperty]
        public partial string FfmpegInputArgs { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string FfmpegOutputArgs { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool TrimStart { get; set; }

        [ObservableProperty]
        public partial bool TrimEnd { get; set; }

        [ObservableProperty]
        public partial int StartHour { get; set; }

        [ObservableProperty]
        public partial int StartMinute { get; set; }

        [ObservableProperty]
        public partial int StartSecond { get; set; }

        [ObservableProperty]
        public partial int EndHour { get; set; }

        [ObservableProperty]
        public partial int EndMinute { get; set; }

        [ObservableProperty]
        public partial int EndSecond { get; set; }

        [ObservableProperty]
        public partial decimal TrimHourMaximum { get; set; } = TrimLimits.DEFAULT_HOUR_MAXIMUM;

        [ObservableProperty]
        public partial string LengthText { get; set; } = "00:00:00";

        [ObservableProperty]
        public partial string InfoStreamer { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string InfoCreatedAt { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string InfoTitle { get; set; } = string.Empty;

        [ObservableProperty]
        public partial byte[]? ThumbnailBytes { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStreamerAvatar))]
        public partial byte[]? StreamerAvatarBytes { get; set; }

        [ObservableProperty]
        public partial string LogText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LogToggleText))]
        public partial bool IsLogExpanded { get; set; }

        public string LogToggleText => IsLogExpanded
            ? Loc.Get("common.hide_log")
            : Loc.Get("common.show_log");

        public string SuggestedFileDisplay => !string.IsNullOrWhiteSpace(SuggestedFileName)
            ? Loc.Get("common.suggested_file", SuggestedFileName)
            : string.Empty;

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            if (InfoLoaded)
            {
                InfoCreatedAt = _hasCreatedAt
                    ? _videoTime.ToString(CultureInfo.CurrentCulture)
                    : Loc.Get("common.unknown");
            }

            Notify(nameof(LogToggleText), nameof(SuggestedFileDisplay));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSuggestedFileName))]
        [NotifyPropertyChangedFor(nameof(SuggestedFileDisplay))]
        public partial string SuggestedFileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Status { get; set; }

        [ObservableProperty]
        public partial double Progress { get; set; }

        public AppStatus AppStatus { get; }

        [ObservableProperty]
        public partial bool InfoLoaded { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; private set; }

        public bool CanBrowse => !IsBusy;
        public bool CanEditOptions => true;
        public bool CanEditTrimStart => CanEditOptions && InfoLoaded && TrimStart;
        public bool CanEditTrimEnd => CanEditOptions && InfoLoaded && TrimEnd;
        public bool CanRender => InfoLoaded && !string.IsNullOrWhiteSpace(InputFile);
        public bool CanCancelQueued => _queued?.CanCancel == true;
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };

        [RelayCommand(CanExecute = nameof(CanBrowse))]
        private async Task BrowseAsync()
        {
            var path = await _fileDialogs.OpenFileAsync(
                Loc.Get("dialogs.open_chat_json"),
                Loc.Get("dialogs.filter_json"),
                ["*.json", "*.json.gz"]);

            if (string.IsNullOrWhiteSpace(path))
                return;

            await LoadFileAsync(path);
        }

        [RelayCommand(CanExecute = nameof(CanBrowse))]
        private async Task LoadTypedFileAsync()
        {
            if (string.IsNullOrWhiteSpace(InputFile))
                return;

            await LoadFileAsync(InputFile);
        }

        [RelayCommand(CanExecute = nameof(CanRender))]
        private async Task RenderAsync()
        {
            if (!ValidateInputs(out var error))
            {
                AppendLog(Loc.Error(error));
                await _dialogs.ShowErrorAsync(Loc.Get("render.parse_failed"), error);
                return;
            }

            if (!_ffmpeg.IsAvailable())
            {
                var missing = Loc.Get("render.ffmpeg_missing");
                AppendLog(Loc.Error(missing));
                await _dialogs.ShowErrorAsync(Loc.Get("render.ffmpeg_missing_title"), missing);
                return;
            }

            var extension = SelectedContainer!.Name.ToLowerInvariant();
            var suggestedName = BuildSuggestedFileName(extension);
            var path = await _fileDialogs.SaveFileAsync(suggestedName, Loc.Get("dialogs.filter_generic", SelectedContainer.Name), extension);
            if (string.IsNullOrWhiteSpace(path))
                return;

            PersistSettings();
            SaveFfmpegArgs();

            var options = BuildOptions(path);
            var item = _queue.EnqueueChatRender(options, _title, ThumbnailBytes);
            TrackQueued(item);

            AppendLog(Loc.Get("common.added_to_queue", path));
        }

        [RelayCommand(CanExecute = nameof(CanCancelQueued))]
        private void Cancel()
        {
            if (_queued is not null)
                _queue.Cancel(_queued);
        }

        [RelayCommand]
        private void ToggleLog()
        {
            IsLogExpanded = !IsLogExpanded;
        }

        [RelayCommand(CanExecute = nameof(CanSavePreset))]
        private async Task SavePresetAsync()
        {
            var name = PresetName.Trim();
            if (name.Length == 0)
                return;

            PersistSettings();
            SaveFfmpegArgs();

            var presets = _settings.Current.RenderPresets;
            var existing = presets.FirstOrDefault(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var overwrite = await _dialogs.ShowConfirmAsync(Loc.Get("render.preset_save"), Loc.Get("render.preset_exists"));
                if (!overwrite)
                    return;

                existing.Name = name;
                existing.Settings = SettingsCopy.Clone(_settings.Current.Render);
            }
            else
            {
                presets.Add(new NamedRenderPreset
                {
                    Name = name,
                    Settings = SettingsCopy.Clone(_settings.Current.Render),
                });
            }

            _settings.Save();
            RefreshPresets();

            _suppressPreset = true;
            SelectedPresetName = name;
            PresetName = name;
            _suppressPreset = false;
        }

        [RelayCommand(CanExecute = nameof(CanDeletePreset))]
        private void DeletePreset()
        {
            var name = SelectedPresetName;
            if (string.IsNullOrWhiteSpace(name))
                return;

            _settings.Current.RenderPresets.RemoveAll(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            _settings.Save();
            PresetName = string.Empty;
            RefreshPresets();
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogText = string.Empty;
            IsLogExpanded = false;
        }

        [RelayCommand]
        private void ResetFfmpegArgs()
        {
            if (SelectedCodec is null)
                return;

            FfmpegInputArgs = SelectedCodec.InputArgs;
            FfmpegOutputArgs = SelectedCodec.OutputArgs;
            SaveFfmpegArgs();
        }

        [RelayCommand]
        private void OpenFfmpegDocs()
        {
            Process.Start(new ProcessStartInfo("https://ffmpeg.org/ffmpeg.html") { UseShellExecute = true });
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName is null)
                return;

            if (e.PropertyName is nameof(SelectedContainer))
                RefreshCodecs(SelectedContainer);

            if (e.PropertyName is nameof(IsBusy) or nameof(InfoLoaded) or nameof(InputFile) or nameof(TrimStart) or nameof(TrimEnd))
                NotifyState();

            if (e.PropertyName is nameof(TrimStart) or nameof(TrimEnd)
                or nameof(StartHour) or nameof(StartMinute) or nameof(StartSecond)
                or nameof(EndHour) or nameof(EndMinute) or nameof(EndSecond)
                or nameof(SelectedContainer) or nameof(InfoLoaded))
                UpdateSuggestedFileName();

            if (_loading)
                return;

            switch (e.PropertyName)
            {
                case nameof(SelectedContainer):
                    PersistSettings();
                    break;
                case nameof(SelectedCodec):
                    PersistSettings();
                    LoadFfmpegArgs();
                    break;
                case nameof(FfmpegInputArgs):
                case nameof(FfmpegOutputArgs):
                    SaveFfmpegArgs();
                    break;
                default:
                    if (SettingProperties.Contains(e.PropertyName))
                        PersistSettings();
                    break;
            }
        }

        private async Task LoadFileAsync(string path)
        {
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog(Loc.Error(Loc.Get("update.unsupported")));
                await _dialogs.ShowErrorAsync(Loc.Get("update.unsupported_title"), Loc.Get("update.unsupported_message"));
                return;
            }

            if (!File.Exists(path))
            {
                AppendLog(Loc.Error(Loc.Get("render.file_not_found") + " " + Path.GetFileName(path)));
                await _dialogs.ShowErrorAsync(Loc.Get("render.file_not_found"), path);
                return;
            }

            InfoLoaded = false;
            _chatJson = null;
            ThumbnailBytes = null;
            StreamerAvatarBytes = null;
            NotifyState();

            IsBusy = true;
            try
            {
                InputFile = path;
                _chatJson = await ChatJson.DeserializeAsync(path, true, true, false);
                ApplyChatInfo(_chatJson);
                InfoLoaded = true;
                UpdateSuggestedFileName();
                NotifyState();
                AppendLog(Loc.Get("common.loaded_file", InfoTitle, Path.GetFileName(path)));
                await TryRefreshMetadataAsync();
            }
            catch (Exception ex)
            {
                InputFile = path;
                AppendLog(Loc.Error(ex.Message));
                await _dialogs.ShowErrorAsync(Loc.Get("update.read_failed"), ex.Message);
                if (_settings.Current.General.VerboseErrors)
                    await _dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyChatInfo(ChatRoot chat)
        {
            var firstComment = chat.comments?.FirstOrDefault();
            var videoCreatedAt = chat.video?.created_at == null && firstComment is not null
                ? firstComment.created_at - TimeSpan.FromSeconds(firstComment.content_offset_seconds)
                : chat.video?.created_at ?? default;

            _videoTime = _settings.Current.General.UtcVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
            _hasCreatedAt = videoCreatedAt != default;
            InfoCreatedAt = _hasCreatedAt
                ? _videoTime.ToString(CultureInfo.CurrentCulture)
                : Loc.Get("common.unknown");

            _streamerName = chat.streamer?.name ?? Loc.Get("common.unknown_user");
            _streamerId = chat.streamer?.id.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            InfoStreamer = _streamerName;
            _title = !string.IsNullOrWhiteSpace(chat.video?.title)
                ? chat.video.title
                : Loc.Get("common.unknown");

            InfoTitle = _title;
            _clipperName = chat.clipper?.name ?? string.Empty;
            _clipperId = chat.clipper is null ? string.Empty : chat.clipper.id.ToString(CultureInfo.InvariantCulture);
            _viewCount = chat.video?.viewCount ?? 0;
            _game = chat.video?.game
                ?? chat.video?.chapters?.FirstOrDefault()?.gameDisplayName
                ?? Loc.Get("common.unknown_game");

            _videoId = chat.video?.id ?? firstComment?.content_id ?? "-1";
            _chatStartSeconds = chat.video is not null && !double.IsNegative(chat.video.start) ? chat.video.start : 0;

            var chatStart = TimeSpan.FromSeconds(_chatStartSeconds);
            StartHour = (int)chatStart.TotalHours;
            StartMinute = chatStart.Minutes;
            StartSecond = chatStart.Seconds;

            var chatEnd = TimeSpan.FromSeconds(chat.video?.end ?? 0);
            EndHour = (int)chatEnd.TotalHours;
            EndMinute = chatEnd.Minutes;
            EndSecond = chatEnd.Seconds;

            _videoLength = TimeSpan.FromSeconds(chat.video is not null && !double.IsNegative(chat.video.length) ? chat.video.length : 0);
            LengthText = _videoLength.TotalSeconds > 0
                ? _videoLength.ToString("c")
                : Loc.Get("common.unknown");

            TrimHourMaximum = TrimLimits.HourMaximum(_videoLength);
            TrimStart = false;
            TrimEnd = false;
        }

        private async Task TryRefreshMetadataAsync()
        {
            if (_videoId is "-1" or "")
                return;

            try
            {
                if (_videoId.All(char.IsDigit) && long.TryParse(_videoId, out var videoId))
                {
                    var videoInfo = await TwitchHelper.GetVideoInfo(videoId);
                    var video = videoInfo.data.video;
                    if (video is null)
                    {
                        AppendLog(Loc.Error(Loc.Get("update.thumbnail_missing")));
                        ThumbnailBytes = await _thumbnails.TryGetAsync(null);
                        TrimHourMaximum = TrimLimits.DEFAULT_HOUR_MAXIMUM;
                        return;
                    }

                    _videoLength = TimeSpan.FromSeconds(video.lengthSeconds);
                    LengthText = _videoLength.ToString("c");
                    TrimHourMaximum = TrimLimits.HourMaximum(_videoLength);
                    _viewCount = video.viewCount;
                    _game = video.game?.displayName ?? _game;
                    ThumbnailBytes = await _thumbnails.TryGetAsync(video.thumbnailURLs.FirstOrDefault());
                    StreamerAvatarBytes = await _thumbnails.TryGetAsync(video.owner?.profileImageURL);
                    UpdateSuggestedFileName();
                    return;
                }

                if (_videoId != "-1")
                    TrimHourMaximum = 0;

                var clipInfo = await TwitchHelper.GetClipInfo(_videoId);
                var clip = clipInfo.data.clip;
                if (clip?.video is null)
                {
                    AppendLog(Loc.Error(Loc.Get("update.thumbnail_missing")));
                    ThumbnailBytes = await _thumbnails.TryGetAsync(null);
                    return;
                }

                _videoLength = TimeSpan.FromSeconds(clip.durationSeconds);
                LengthText = _videoLength.ToString("c");
                _viewCount = clip.viewCount;
                _game = clip.game?.displayName ?? _game;
                if (string.IsNullOrEmpty(_clipperName))
                    _clipperName = clip.curator?.displayName ?? Loc.Get("common.unknown_user");

                if (string.IsNullOrEmpty(_clipperId))
                    _clipperId = clip.curator?.id ?? string.Empty;

                ThumbnailBytes = await _thumbnails.TryGetAsync(clip.thumbnailURL);
                StreamerAvatarBytes = await _thumbnails.TryGetAsync(clip.broadcaster?.profileImageURL);
                UpdateSuggestedFileName();
            }
            catch (Exception ex)
            {
                AppendLog(Loc.Error(ex.Message));
                await _dialogs.ShowErrorAsync(Loc.Get("update.get_info_failed"), ex.Message);
                if (_settings.Current.General.VerboseErrors)
                    await _dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());
            }
        }

        private bool ValidateInputs(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(InputFile) || !File.Exists(InputFile))
            {
                error = Loc.Get("render.no_json");
                return false;
            }

            if (SelectedContainer is null || SelectedCodec is null)
            {
                error = Loc.Get("render.select_codec");
                return false;
            }

            if (!TryParseColor(FontColorHex, out var fontColor)
                || !TryParseColor(BackgroundColorHex, out var background)
                || !TryParseColor(AlternateBackgroundColorHex, out var alternate)
                || !TryParseColor(HighlightUsersColorHex, out _))
            {
                error = Loc.Get("render.invalid_colors");
                return false;
            }

            if (ChatWidth < 2 || ChatHeight < 2)
            {
                error = Loc.Get("render.size_min");
                return false;
            }

            if (ChatWidth % 2 != 0 || ChatHeight % 2 != 0)
            {
                error = Loc.Get("render.size_even");
                return false;
            }

            if (Framerate < 1)
            {
                error = Loc.Get("render.fps_min");
                return false;
            }

            if (UpdateRate < 0)
            {
                error = Loc.Get("render.update_rate_negative");
                return false;
            }

            if (!GenerateMask && (background.Alpha < 255 || (AlternateMessageBackgrounds && alternate.Alpha < 255)))
            {
                var container = SelectedContainer.Name;
                var codec = SelectedCodec.Name;
                if (container is not ("MOV" or "WEBM") || codec is not ("RLE" or "ProRes" or "VP8" or "VP9"))
                {
                    error = Loc.Get("render.alpha_unsupported");
                    return false;
                }
            }

            if (GenerateMask && background.Alpha == 255 && !(AlternateMessageBackgrounds && alternate.Alpha != 255))
            {
                error = Loc.Get("render.mask_no_alpha");
                return false;
            }

            _ = fontColor;
            return true;
        }

        private ChatRenderOptions BuildOptions(string outputFile)
        {
            return ChatRenderOptionsFactory.Create(Loc, new ChatRenderBuildArgs
            {
                InputFile = InputFile,
                OutputFile = outputFile,
                BackgroundColorHex = BackgroundColorHex,
                AlternateBackgroundColorHex = AlternateBackgroundColorHex,
                FontColorHex = FontColorHex,
                HighlightUsersColorHex = HighlightUsersColorHex,
                AlternateMessageBackgrounds = AlternateMessageBackgrounds,
                ChatHeight = ChatHeight,
                ChatWidth = ChatWidth,
                BttvEmotes = BttvEmotes,
                FfzEmotes = FfzEmotes,
                StvEmotes = StvEmotes,
                Outline = Outline,
                Font = SelectedFont ?? "Inter Embedded",
                FontSize = FontSize,
                UpdateRate = UpdateRate,
                EmoteScale = EmoteScale,
                BadgeScale = BadgeScale,
                EmojiScale = EmojiScale,
                AvatarScale = AvatarScale,
                SidePaddingScale = SidePaddingScale,
                SectionHeightScale = SectionHeightScale,
                WordSpacingScale = WordSpacingScale,
                EmoteSpacingScale = EmoteSpacingScale,
                AccentIndentScale = AccentIndentScale,
                AccentStrokeScale = AccentStrokeScale,
                VerticalSpacingScale = VerticalSpacingScale,
                UsernameFontScale = UsernameFontScale,
                OutlineScale = OutlineScale,
                HighlightUsersList = HighlightUsersList,
                IgnoreUsersList = IgnoreUsersList,
                BannedWordsList = BannedWordsList,
                Timestamp = Timestamp,
                Framerate = Framerate,
                FfmpegInputArgs = FfmpegInputArgs,
                FfmpegOutputArgs = FfmpegOutputArgs,
                Sharpening = Sharpening,
                GenerateMask = GenerateMask,
                TempFolder = _settings.Current.General.TempPath,
                SubMessages = SubMessages,
                ChatBadges = ChatBadges,
                Offline = Offline,
                RenderUserAvatars = RenderUserAvatars,
                DisperseCommentOffsets = DisperseCommentOffsets,
                AdjustUsernameVisibility = AdjustUsernameVisibility,
                EmojiVendor = EmojiVendor,
                ChatBadgeMask = BuildBadgeMask(),
                StartOverride = TrimStart ? (int)Math.Round(StartTime.TotalSeconds) : -1,
                EndOverride = TrimEnd ? (int)Math.Round(EndTime.TotalSeconds) : -1,
            }, _ffmpeg.ResolvedPath, file => _collision.HandleCollision(file)!);
        }

        private void OnSettingsReloaded(object? sender, EventArgs e)
        {
            _loading = true;
            try
            {
                LoadFromSettings();
                LoadFfmpegArgs();
                RefreshPresets();
            }
            finally
            {
                _loading = false;
            }
        }

        private void RefreshPresets()
        {
            var selected = SelectedPresetName;
            _suppressPreset = true;
            PresetNames.Clear();

            foreach (var preset in _settings.Current.RenderPresets.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
                PresetNames.Add(preset.Name);

            SelectedPresetName = selected is not null && PresetNames.Contains(selected) ? selected : null;
            _suppressPreset = false;
            DeletePresetCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPresetNameChanged(string? value)
        {
            if (_suppressPreset || string.IsNullOrWhiteSpace(value))
                return;

            var preset = _settings.Current.RenderPresets.FirstOrDefault(item => item.Name == value);
            if (preset is null)
                return;

            _loading = true;
            try
            {
                _settings.Current.Render = SettingsCopy.Clone(preset.Settings);
                LoadFromSettings();
                LoadFfmpegArgs();
                PresetName = preset.Name;
            }
            finally
            {
                _loading = false;
            }

            PersistSettings();
        }

        private void LoadFromSettings()
        {
            var render = _settings.Current.Render;
            var chat = _settings.Current.Chat;
            SelectedFont = !Fonts.Contains(render.Font)
                ? Fonts.FirstOrDefault(font => font == "Inter Embedded") ?? Fonts.FirstOrDefault()
                : render.Font;

            if (SelectedFont is null && !string.IsNullOrWhiteSpace(render.Font))
            {
                Fonts.Add(render.Font);
                SelectedFont = render.Font;
            }

            FontSize = render.FontSize;
            ChatWidth = render.Width;
            ChatHeight = render.Height;
            FontColorHex = render.FontColor;
            BackgroundColorHex = render.BackgroundColor;
            AlternateBackgroundColorHex = render.AlternateBackgroundColor;
            HighlightUsersColorHex = render.HighlightUsersColor;
            Outline = render.Outline;
            Timestamp = render.Timestamp;
            SubMessages = render.SubMessages;
            ChatBadges = render.ChatBadges;
            UpdateRate = render.UpdateTime;
            DisperseCommentOffsets = render.DisperseCommentOffsets;
            AlternateMessageBackgrounds = render.AlternateMessageBackgrounds;
            AdjustUsernameVisibility = render.AdjustUsernameVisibility;
            BttvEmotes = chat.BttvEmotes;
            FfzEmotes = chat.FfzEmotes;
            StvEmotes = chat.StvEmotes;
            RenderUserAvatars = render.UserAvatars;
            Offline = render.Offline;
            IgnoreUsersList = render.IgnoreUsersList;
            BannedWordsList = render.BannedWordsList;
            HighlightUsersList = render.HighlightUsersList;
            EmojiVendor = Enum.IsDefined(typeof(EmojiVendor), render.EmojiVendor)
                ? (EmojiVendor)render.EmojiVendor
                : EmojiVendor.GoogleNotoColor;

            var mask = (ChatBadgeType)render.ChatBadgeMask;
            FilterBroadcaster = mask.HasFlag(ChatBadgeType.Broadcaster);
            FilterModerator = mask.HasFlag(ChatBadgeType.Moderator);
            FilterVip = mask.HasFlag(ChatBadgeType.VIP);
            FilterSubscriber = mask.HasFlag(ChatBadgeType.Subscriber);
            FilterPredictions = mask.HasFlag(ChatBadgeType.Predictions);
            FilterNoAudioVisual = mask.HasFlag(ChatBadgeType.NoAudioVisual);
            FilterPrimeGaming = mask.HasFlag(ChatBadgeType.PrimeGaming);
            FilterOther = mask.HasFlag(ChatBadgeType.Other);

            EmoteScale = render.EmoteScale;
            BadgeScale = render.BadgeScale;
            EmojiScale = render.EmojiScale;
            AvatarScale = render.AvatarScale;
            OutlineScale = render.OutlineScale;
            UsernameFontScale = render.UsernameFontScale;
            VerticalSpacingScale = render.VerticalSpacingScale;
            SectionHeightScale = render.SectionHeightScale;
            WordSpacingScale = render.WordSpacingScale;
            EmoteSpacingScale = render.EmoteSpacingScale;
            AccentStrokeScale = render.AccentStrokeScale;
            AccentIndentScale = render.AccentIndentScale;
            SidePaddingScale = render.SidePaddingScale;
            Framerate = render.Framerate;
            GenerateMask = render.GenerateMask;
            Sharpening = render.Sharpening;
            SelectedContainer = Containers.FirstOrDefault(container => container.Name == render.VideoContainer) ?? Containers.FirstOrDefault();
        }

        private void PersistSettings()
        {
            if (_loading)
                return;

            var render = _settings.Current.Render;
            var chat = _settings.Current.Chat;
            render.Font = SelectedFont ?? "Inter Embedded";
            render.FontSize = FontSize;
            render.Width = ChatWidth;
            render.Height = ChatHeight;
            render.FontColor = FontColorHex;
            render.BackgroundColor = BackgroundColorHex;
            render.AlternateBackgroundColor = AlternateBackgroundColorHex;
            render.HighlightUsersColor = HighlightUsersColorHex;
            render.Outline = Outline;
            render.Timestamp = Timestamp;
            render.SubMessages = SubMessages;
            render.ChatBadges = ChatBadges;
            render.UpdateTime = UpdateRate;
            render.DisperseCommentOffsets = DisperseCommentOffsets;
            render.AlternateMessageBackgrounds = AlternateMessageBackgrounds;
            render.AdjustUsernameVisibility = AdjustUsernameVisibility;
            chat.BttvEmotes = BttvEmotes;
            chat.FfzEmotes = FfzEmotes;
            chat.StvEmotes = StvEmotes;
            render.UserAvatars = RenderUserAvatars;
            render.Offline = Offline;
            render.IgnoreUsersList = JoinCsv(IgnoreUsersList);
            render.BannedWordsList = JoinCsv(BannedWordsList);
            render.HighlightUsersList = JoinCsv(HighlightUsersList);
            render.EmojiVendor = (int)EmojiVendor;
            render.ChatBadgeMask = (int)BuildBadgeMask();
            render.EmoteScale = EmoteScale;
            render.BadgeScale = BadgeScale;
            render.EmojiScale = EmojiScale;
            render.AvatarScale = AvatarScale;
            render.OutlineScale = OutlineScale;
            render.UsernameFontScale = UsernameFontScale;
            render.VerticalSpacingScale = VerticalSpacingScale;
            render.SectionHeightScale = SectionHeightScale;
            render.WordSpacingScale = WordSpacingScale;
            render.EmoteSpacingScale = EmoteSpacingScale;
            render.AccentStrokeScale = AccentStrokeScale;
            render.AccentIndentScale = AccentIndentScale;
            render.SidePaddingScale = SidePaddingScale;
            render.Framerate = Framerate;
            render.GenerateMask = GenerateMask;
            render.Sharpening = Sharpening;

            if (SelectedContainer is not null)
                render.VideoContainer = SelectedContainer.Name;

            if (SelectedCodec is not null)
                render.VideoCodec = SelectedCodec.Name;

            _settings.Save();
        }

        private void RefreshCodecs(RenderContainer? container)
        {
            var preferred = _settings.Current.Render.VideoCodec;
            Codecs.Clear();
            if (container is null)
            {
                SelectedCodec = null;
                return;
            }

            foreach (var codec in container.Codecs)
                Codecs.Add(codec);

            SelectedCodec = Codecs.FirstOrDefault(codec => codec.Name == preferred) ?? Codecs.FirstOrDefault();
        }

        private void LoadFfmpegArgs()
        {
            if (SelectedContainer is null || SelectedCodec is null)
                return;

            var match = DeserializeFfmpegArgs()
                .FirstOrDefault(arg => arg.CodecName == SelectedCodec.Name && arg.ContainerName == SelectedContainer.Name);

            _loadingFfmpeg = true;
            FfmpegInputArgs = string.IsNullOrWhiteSpace(match?.InputArgs) ? SelectedCodec.InputArgs : match.InputArgs;
            FfmpegOutputArgs = string.IsNullOrWhiteSpace(match?.OutputArgs) ? SelectedCodec.OutputArgs : match.OutputArgs;
            _loadingFfmpeg = false;
        }

        private void SaveFfmpegArgs()
        {
            if (_loading || _loadingFfmpeg || SelectedContainer is null || SelectedCodec is null)
                return;

            var args = DeserializeFfmpegArgs();
            var match = args.FirstOrDefault(arg =>
                arg.CodecName == SelectedCodec.Name && arg.ContainerName == SelectedContainer.Name);

            if (match is null)
            {
                match = new CustomFfmpegArgs
                {
                    CodecName = SelectedCodec.Name,
                    ContainerName = SelectedContainer.Name,
                };
                args.Add(match);
            }

            match.InputArgs = FfmpegInputArgs;
            match.OutputArgs = FfmpegOutputArgs;

            _settings.Current.Render.FfmpegArguments = JsonSerializer.Serialize(args);
            _settings.Save();
        }

        private List<CustomFfmpegArgs> DeserializeFfmpegArgs()
        {
            try
            {
                return JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(_settings.Current.Render.FfmpegArguments) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private ChatBadgeType BuildBadgeMask()
        {
            ChatBadgeType mask = 0;
            if (FilterBroadcaster) mask |= ChatBadgeType.Broadcaster;
            if (FilterModerator) mask |= ChatBadgeType.Moderator;
            if (FilterVip) mask |= ChatBadgeType.VIP;
            if (FilterSubscriber) mask |= ChatBadgeType.Subscriber;
            if (FilterPredictions) mask |= ChatBadgeType.Predictions;
            if (FilterNoAudioVisual) mask |= ChatBadgeType.NoAudioVisual;
            if (FilterPrimeGaming) mask |= ChatBadgeType.PrimeGaming;
            if (FilterOther) mask |= ChatBadgeType.Other;
            return mask;
        }

        private TimeSpan StartTime => new(StartHour, StartMinute, StartSecond);
        private TimeSpan EndTime => new(EndHour, EndMinute, EndSecond);

        private void UpdateSuggestedFileName()
        {
            if (!InfoLoaded || SelectedContainer is null)
            {
                SuggestedFileName = string.Empty;
                return;
            }

            SuggestedFileName = BuildSuggestedFileName(SelectedContainer.Name.ToLowerInvariant());
        }

        private string BuildSuggestedFileName(string extension)
        {
            var trimStart = !TrimStart
                ? TimeSpan.FromSeconds(_chatStartSeconds)
                : StartTime;

            var trimEnd = TrimEnd ? EndTime : _videoLength;
            var name = FilenameService.GetFilename(
                _settings.Current.General.TemplateChat,
                _title,
                _videoId,
                _videoTime,
                _streamerName,
                _streamerId,
                trimStart,
                trimEnd,
                _videoLength,
                _viewCount,
                _game,
                !string.IsNullOrEmpty(_clipperName) ? _clipperName : null,
                !string.IsNullOrEmpty(_clipperId) ? _clipperId : null);

            return name + "." + extension.TrimStart('.');
        }

        private void NotifyState()
        {
            OnPropertyChanged(nameof(CanBrowse));
            OnPropertyChanged(nameof(CanEditOptions));
            OnPropertyChanged(nameof(CanEditTrimStart));
            OnPropertyChanged(nameof(CanEditTrimEnd));
            OnPropertyChanged(nameof(CanRender));

            BrowseCommand.NotifyCanExecuteChanged();
            LoadTypedFileCommand.NotifyCanExecuteChanged();
            RenderCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanCancelQueued));
        }

        private void TrackQueued(QueueItemViewModel item)
        {
            if (_queued is not null)
                _queued.PropertyChanged -= OnQueuedChanged;

            _queued = item;
            _queued.PropertyChanged += OnQueuedChanged;
            Status = item.DisplayStatus;
            Progress = item.Progress;
            NotifyState();
        }

        private void OnQueuedChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_queued is null)
                return;

            if (e.PropertyName is null or nameof(QueueItemViewModel.DisplayStatus))
                Status = _queued.DisplayStatus;
            if (e.PropertyName is null or nameof(QueueItemViewModel.Progress))
                Progress = _queued.Progress;
            if (e.PropertyName is null or nameof(QueueItemViewModel.CanCancel) or nameof(QueueItemViewModel.Status))
                NotifyState();
        }

        private void AppendLog(string message)
        {
            var builder = new StringBuilder(LogText);
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(message);
            LogText = builder.ToString();
        }

        private static List<string> LoadFonts()
        {
            var fonts = SKFontManager.Default.FontFamilies.ToList();
            if (!fonts.Contains("Inter Embedded", StringComparer.OrdinalIgnoreCase))
                fonts.Add("Inter Embedded");

            fonts.Sort(StringComparer.OrdinalIgnoreCase);
            return fonts;
        }

        private static string[] SplitCsv(string value)
        {
            return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        private static string JoinCsv(string value) => string.Join(",", SplitCsv(value));

        private static bool TryParseColor(string hex, out SKColor color)
        {
            return ChatRenderOptionsFactory.TryParseColor(hex, out color);
        }
    }
}
