using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects;
using Color = Avalonia.Media.Color;

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
        private readonly DialogService _dialogs;
        private readonly FileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private readonly QueueService _queue;
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
        private readonly bool _loading;
        private bool _loadingFfmpeg;

        public ChatRenderViewModel(
            SettingsService settings,
            AppStatus appStatus,
            FfmpegService ffmpeg,
            DialogService dialogs,
            FileDialogService fileDialogs,
            FileCollisionService collision,
            ThumbnailService thumbnails,
            QueueService queue)
        {
            _settings = settings;
            AppStatus = appStatus;
            _ffmpeg = ffmpeg;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _collision = collision;
            _thumbnails = thumbnails;
            _queue = queue;

            foreach (var font in LoadFonts())
                Fonts.Add(font);

            foreach (var container in RenderEncodingPresets.CreateContainers())
                Containers.Add(container);

            LoadFromSettings();
            _loading = false;
            LoadFfmpegArgs();
            Status = "Idle";
        }

        public ObservableCollection<string> Fonts { get; } = [];
        public ObservableCollection<RenderContainer> Containers { get; } = [];
        public ObservableCollection<RenderCodec> Codecs { get; } = [];

        [ObservableProperty]
        public partial string InputFile { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? SelectedFont { get; set; }

        [ObservableProperty]
        public partial double FontSize { get; set; }

        [ObservableProperty]
        public partial int ChatWidth { get; set; }

        [ObservableProperty]
        public partial int ChatHeight { get; set; }

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
        public partial bool SubMessages { get; set; }

        [ObservableProperty]
        public partial bool ChatBadges { get; set; }

        [ObservableProperty]
        public partial double UpdateRate { get; set; }

        [ObservableProperty]
        public partial bool DisperseCommentOffsets { get; set; }

        [ObservableProperty]
        public partial bool AlternateMessageBackgrounds { get; set; }

        [ObservableProperty]
        public partial bool AdjustUsernameVisibility { get; set; }

        [ObservableProperty]
        public partial bool BttvEmotes { get; set; }

        [ObservableProperty]
        public partial bool FfzEmotes { get; set; }

        [ObservableProperty]
        public partial bool StvEmotes { get; set; }

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
        public partial EmojiVendor EmojiVendor { get; set; }

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
        public partial int Framerate { get; set; }

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
        public partial decimal TrimHourMaximum { get; set; } = 48;

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

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            Notify(nameof(LogToggleText));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSuggestedFileName))]
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
                if (_settings.Current.VerboseErrors)
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

            _videoTime = _settings.Current.UtcVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
            InfoCreatedAt = videoCreatedAt != default
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

            TrimHourMaximum = _videoLength > TimeSpan.Zero ? (int)_videoLength.TotalHours : 48;
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
                        TrimHourMaximum = 48;
                        return;
                    }

                    _videoLength = TimeSpan.FromSeconds(video.lengthSeconds);
                    LengthText = _videoLength.ToString("c");
                    TrimHourMaximum = _videoLength > TimeSpan.Zero ? (int)_videoLength.TotalHours : 48;
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
                if (_settings.Current.VerboseErrors)
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
            var background = RequireColor(BackgroundColorHex);
            var alternate = RequireColor(AlternateBackgroundColorHex);
            var fontColor = RequireColor(FontColorHex);
            var highlight = RequireColor(HighlightUsersColorHex);

            var inputArgs = Sharpening
                ? FfmpegInputArgs + " -filter_complex \"smartblur=lr=1:ls=-1.0\""
                : FfmpegInputArgs;

            return new ChatRenderOptions
            {
                OutputFile = outputFile,
                InputFile = InputFile,
                BackgroundColor = background,
                AlternateBackgroundColor = alternate,
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
                HighlightUserColor = highlight,
                HighlightUsersArray = SplitCsv(HighlightUsersList),
                IgnoreUsersArray = SplitCsv(IgnoreUsersList),
                BannedWordsArray = SplitCsv(BannedWordsList),
                Timestamp = Timestamp,
                MessageColor = fontColor.WithAlpha(255),
                Framerate = Framerate,
                InputArgs = inputArgs,
                OutputArgs = FfmpegOutputArgs,
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                GenerateMask = GenerateMask,
                OutlineSize = 4 * OutlineScale,
                FfmpegPath = _ffmpeg.ResolvedPath,
                TempFolder = _settings.Current.TempPath,
                SubMessages = SubMessages,
                ChatBadges = ChatBadges,
                Offline = Offline,
                RenderUserAvatars = RenderUserAvatars,
                AllowUnlistedEmotes = true,
                DisperseCommentOffsets = DisperseCommentOffsets,
                AdjustUsernameVisibility = AdjustUsernameVisibility,
                EmojiVendor = EmojiVendor,
                ChatBadgeMask = BuildBadgeMask(),
                StartOverride = TrimStart ? (int)Math.Round(StartTime.TotalSeconds) : -1,
                EndOverride = TrimEnd ? (int)Math.Round(EndTime.TotalSeconds) : -1,
                FileCollisionCallback = file => _collision.HandleCollision(file),
            };
        }

        private void LoadFromSettings()
        {
            var current = _settings.Current;
            SelectedFont = !Fonts.Contains(current.RenderFont)
                ? Fonts.FirstOrDefault(font => font == "Inter Embedded") ?? Fonts.FirstOrDefault()
                : current.RenderFont;

            if (SelectedFont is null && !string.IsNullOrWhiteSpace(current.RenderFont))
            {
                Fonts.Add(current.RenderFont);
                SelectedFont = current.RenderFont;
            }

            FontSize = current.RenderFontSize;
            ChatWidth = current.RenderWidth;
            ChatHeight = current.RenderHeight;
            FontColorHex = current.RenderFontColor;
            BackgroundColorHex = current.RenderBackgroundColor;
            AlternateBackgroundColorHex = current.RenderAlternateBackgroundColor;
            HighlightUsersColorHex = current.RenderHighlightUsersColor;
            Outline = current.RenderOutline;
            Timestamp = current.RenderTimestamp;
            SubMessages = current.RenderSubMessages;
            ChatBadges = current.RenderChatBadges;
            UpdateRate = current.RenderUpdateTime;
            DisperseCommentOffsets = current.RenderDisperseCommentOffsets;
            AlternateMessageBackgrounds = current.RenderAlternateMessageBackgrounds;
            AdjustUsernameVisibility = current.RenderAdjustUsernameVisibility;
            BttvEmotes = current.BttvEmotes;
            FfzEmotes = current.FfzEmotes;
            StvEmotes = current.StvEmotes;
            RenderUserAvatars = current.RenderUserAvatars;
            Offline = current.RenderOffline;
            IgnoreUsersList = current.RenderIgnoreUsersList;
            BannedWordsList = current.RenderBannedWordsList;
            HighlightUsersList = current.RenderHighlightUsersList;
            EmojiVendor = Enum.IsDefined(typeof(EmojiVendor), current.RenderEmojiVendor)
                ? (EmojiVendor)current.RenderEmojiVendor
                : EmojiVendor.GoogleNotoColor;

            var mask = (ChatBadgeType)current.RenderChatBadgeMask;
            FilterBroadcaster = mask.HasFlag(ChatBadgeType.Broadcaster);
            FilterModerator = mask.HasFlag(ChatBadgeType.Moderator);
            FilterVip = mask.HasFlag(ChatBadgeType.VIP);
            FilterSubscriber = mask.HasFlag(ChatBadgeType.Subscriber);
            FilterPredictions = mask.HasFlag(ChatBadgeType.Predictions);
            FilterNoAudioVisual = mask.HasFlag(ChatBadgeType.NoAudioVisual);
            FilterPrimeGaming = mask.HasFlag(ChatBadgeType.PrimeGaming);
            FilterOther = mask.HasFlag(ChatBadgeType.Other);

            EmoteScale = current.RenderEmoteScale;
            BadgeScale = current.RenderBadgeScale;
            EmojiScale = current.RenderEmojiScale;
            AvatarScale = current.RenderAvatarScale;
            OutlineScale = current.RenderOutlineScale;
            UsernameFontScale = current.RenderUsernameFontScale;
            VerticalSpacingScale = current.RenderVerticalSpacingScale;
            SectionHeightScale = current.RenderSectionHeightScale;
            WordSpacingScale = current.RenderWordSpacingScale;
            EmoteSpacingScale = current.RenderEmoteSpacingScale;
            AccentStrokeScale = current.RenderAccentStrokeScale;
            AccentIndentScale = current.RenderAccentIndentScale;
            SidePaddingScale = current.RenderSidePaddingScale;
            Framerate = current.RenderFramerate;
            GenerateMask = current.RenderGenerateMask;
            Sharpening = current.RenderSharpening;
            SelectedContainer = Containers.FirstOrDefault(container => container.Name == current.RenderVideoContainer)
                ?? Containers.FirstOrDefault();
        }

        private void PersistSettings()
        {
            if (_loading)
                return;

            var current = _settings.Current;
            current.RenderFont = SelectedFont ?? "Inter Embedded";
            current.RenderFontSize = FontSize;
            current.RenderWidth = ChatWidth;
            current.RenderHeight = ChatHeight;
            current.RenderFontColor = FontColorHex;
            current.RenderBackgroundColor = BackgroundColorHex;
            current.RenderAlternateBackgroundColor = AlternateBackgroundColorHex;
            current.RenderHighlightUsersColor = HighlightUsersColorHex;
            current.RenderOutline = Outline;
            current.RenderTimestamp = Timestamp;
            current.RenderSubMessages = SubMessages;
            current.RenderChatBadges = ChatBadges;
            current.RenderUpdateTime = UpdateRate;
            current.RenderDisperseCommentOffsets = DisperseCommentOffsets;
            current.RenderAlternateMessageBackgrounds = AlternateMessageBackgrounds;
            current.RenderAdjustUsernameVisibility = AdjustUsernameVisibility;
            current.BttvEmotes = BttvEmotes;
            current.FfzEmotes = FfzEmotes;
            current.StvEmotes = StvEmotes;
            current.RenderUserAvatars = RenderUserAvatars;
            current.RenderOffline = Offline;
            current.RenderIgnoreUsersList = JoinCsv(IgnoreUsersList);
            current.RenderBannedWordsList = JoinCsv(BannedWordsList);
            current.RenderHighlightUsersList = JoinCsv(HighlightUsersList);
            current.RenderEmojiVendor = (int)EmojiVendor;
            current.RenderChatBadgeMask = (int)BuildBadgeMask();
            current.RenderEmoteScale = EmoteScale;
            current.RenderBadgeScale = BadgeScale;
            current.RenderEmojiScale = EmojiScale;
            current.RenderAvatarScale = AvatarScale;
            current.RenderOutlineScale = OutlineScale;
            current.RenderUsernameFontScale = UsernameFontScale;
            current.RenderVerticalSpacingScale = VerticalSpacingScale;
            current.RenderSectionHeightScale = SectionHeightScale;
            current.RenderWordSpacingScale = WordSpacingScale;
            current.RenderEmoteSpacingScale = EmoteSpacingScale;
            current.RenderAccentStrokeScale = AccentStrokeScale;
            current.RenderAccentIndentScale = AccentIndentScale;
            current.RenderSidePaddingScale = SidePaddingScale;
            current.RenderFramerate = Framerate;
            current.RenderGenerateMask = GenerateMask;
            current.RenderSharpening = Sharpening;
            if (SelectedContainer is not null)
                current.RenderVideoContainer = SelectedContainer.Name;
            if (SelectedCodec is not null)
                current.RenderVideoCodec = SelectedCodec.Name;

            _settings.Save();
        }

        private void RefreshCodecs(RenderContainer? container)
        {
            var preferred = _settings.Current.RenderVideoCodec;
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
            _settings.Current.RenderFfmpegArguments = JsonSerializer.Serialize(args);
            _settings.Save();
        }

        private List<CustomFfmpegArgs> DeserializeFfmpegArgs()
        {
            try
            {
                return JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(_settings.Current.RenderFfmpegArguments) ?? [];
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
                _settings.Current.TemplateChat,
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

        public void AppendLog(string message)
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

        private static SKColor RequireColor(string hex)
        {
            if (!TryParseColor(hex, out var color))
                throw new InvalidOperationException(Loc.Get("render.invalid_colors"));

            return color;
        }

        private static bool TryParseColor(string hex, out SKColor color)
        {
            color = SKColors.Transparent;
            if (string.IsNullOrWhiteSpace(hex) || !Color.TryParse(hex.Trim(), out var parsed))
                return false;

            color = new SKColor(parsed.R, parsed.G, parsed.B, parsed.A);
            return true;
        }
    }
}
