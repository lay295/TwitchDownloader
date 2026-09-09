using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class ChatUpdateViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
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

        public ChatUpdateViewModel(
            SettingsService settings,
            AppStatus appStatus,
            DialogService dialogs,
            FileDialogService fileDialogs,
            FileCollisionService collision,
            ThumbnailService thumbnails,
            QueueService queue)
        {
            _settings = settings;
            AppStatus = appStatus;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _collision = collision;
            _thumbnails = thumbnails;
            _queue = queue;
            OutputFormat = _settings.Current.ChatDownloadFormat;
            Compression = _settings.Current.ChatJsonCompression;
            TimestampStyle = _settings.Current.ChatTextTimestampStyle;
            EmbedMissing = _settings.Current.ChatEmbedMissing;
            ReplaceEmbeds = _settings.Current.ChatReplaceEmbeds;
            BttvEmotes = _settings.Current.BttvEmotes;
            FfzEmotes = _settings.Current.FfzEmotes;
            StvEmotes = _settings.Current.StvEmotes;
            if (EmbedMissing && ReplaceEmbeds)
                ReplaceEmbeds = false;
            Status = "Idle";
        }

        [ObservableProperty]
        public partial string InputFile { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCompression))]
        [NotifyPropertyChangedFor(nameof(ShowTimestamps))]
        [NotifyPropertyChangedFor(nameof(ShowEmbedOptions))]
        [NotifyPropertyChangedFor(nameof(CanEditThirdPartyEmotes))]
        public partial ChatFormat OutputFormat { get; set; }

        [ObservableProperty]
        public partial ChatCompression Compression { get; set; }

        [ObservableProperty]
        public partial TimestampFormat TimestampStyle { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEditThirdPartyEmotes))]
        public partial bool EmbedMissing { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEditThirdPartyEmotes))]
        public partial bool ReplaceEmbeds { get; set; }

        [ObservableProperty]
        public partial bool BttvEmotes { get; set; }

        [ObservableProperty]
        public partial bool FfzEmotes { get; set; }

        [ObservableProperty]
        public partial bool StvEmotes { get; set; }

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
        public bool CanEditOptions => InfoLoaded;
        public bool CanEditTrimStart => CanEditOptions && TrimStart;
        public bool CanEditTrimEnd => CanEditOptions && TrimEnd;
        public bool CanUpdate => InfoLoaded && !string.IsNullOrWhiteSpace(InputFile);
        public bool CanCancelQueued => _queued?.CanCancel == true;
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };
        public bool ShowCompression => OutputFormat == ChatFormat.Json;
        public bool ShowTimestamps => OutputFormat == ChatFormat.Text;
        public bool ShowEmbedOptions => OutputFormat is ChatFormat.Json or ChatFormat.Html;
        public bool CanEditThirdPartyEmotes => CanEditOptions && ShowEmbedOptions && (EmbedMissing || ReplaceEmbeds);

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

        [RelayCommand(CanExecute = nameof(CanUpdate))]
        private async Task UpdateAsync()
        {
            if (_chatJson is null)
                return;

            var extension = GetFileExtension();
            var suggestedName = BuildSuggestedFileName(extension);
            var (filterName, filterExtension) = GetSaveFilter();
            var path = await _fileDialogs.SaveFileAsync(suggestedName, filterName, filterExtension);
            if (string.IsNullOrWhiteSpace(path))
                return;

            var options = BuildOptions(path);
            var item = _queue.EnqueueChatUpdate(options, _title, ThumbnailBytes);
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

        partial void OnOutputFormatChanged(ChatFormat value)
        {
            _settings.Current.ChatDownloadFormat = value;
            _settings.Save();
            UpdateSuggestedFileName();
        }

        partial void OnCompressionChanged(ChatCompression value)
        {
            _settings.Current.ChatJsonCompression = value;
            _settings.Save();
            UpdateSuggestedFileName();
        }

        partial void OnTimestampStyleChanged(TimestampFormat value)
        {
            _settings.Current.ChatTextTimestampStyle = value;
            _settings.Save();
        }

        partial void OnEmbedMissingChanged(bool value)
        {
            if (value && ReplaceEmbeds)
                ReplaceEmbeds = false;

            _settings.Current.ChatEmbedMissing = value;
            _settings.Save();
            OnPropertyChanged(nameof(CanEditThirdPartyEmotes));
        }

        partial void OnReplaceEmbedsChanged(bool value)
        {
            if (value && EmbedMissing)
                EmbedMissing = false;

            _settings.Current.ChatReplaceEmbeds = value;
            _settings.Save();
            OnPropertyChanged(nameof(CanEditThirdPartyEmotes));
        }

        partial void OnBttvEmotesChanged(bool value)
        {
            _settings.Current.BttvEmotes = value;
            _settings.Save();
        }

        partial void OnFfzEmotesChanged(bool value)
        {
            _settings.Current.FfzEmotes = value;
            _settings.Save();
        }

        partial void OnStvEmotesChanged(bool value)
        {
            _settings.Current.StvEmotes = value;
            _settings.Save();
        }

        partial void OnTrimStartChanged(bool value) => OnTrimEnabledChanged(value);
        partial void OnTrimEndChanged(bool value) => OnTrimEnabledChanged(value);
        partial void OnStartHourChanged(int value) => OnTrimTimeChanged(value);
        partial void OnStartMinuteChanged(int value) => OnTrimTimeChanged(value);
        partial void OnStartSecondChanged(int value) => OnTrimTimeChanged(value);
        partial void OnEndHourChanged(int value) => OnTrimTimeChanged(value);
        partial void OnEndMinuteChanged(int value) => OnTrimTimeChanged(value);
        partial void OnEndSecondChanged(int value) => OnTrimTimeChanged(value);
        partial void OnIsBusyChanged(bool value) => OnBusyOrInfoChanged(value);
        partial void OnInfoLoadedChanged(bool value) => OnBusyOrInfoChanged(value);

        private void OnTrimEnabledChanged(bool _)
        {
            UpdateSuggestedFileName();
            NotifyState();
        }

        private void OnTrimTimeChanged(int _) => UpdateSuggestedFileName();
        private void OnBusyOrInfoChanged(bool _) => NotifyState();

        private async Task LoadFileAsync(string path)
        {
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog(Loc.Error(Loc.Get("update.unsupported")));
                await _dialogs.ShowErrorAsync(Loc.Get("update.unsupported_title"), Loc.Get("update.unsupported_message"));
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
            _title = string.IsNullOrWhiteSpace(chat.video?.title)
                ? Loc.Get("common.unknown")
                : chat.video.title;

            InfoTitle = _title;
            _clipperName = chat.clipper?.name ?? string.Empty;
            _clipperId = chat.clipper is not null
                ? chat.clipper.id.ToString(CultureInfo.InvariantCulture)
                : string.Empty;

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
            LengthText = _videoLength.TotalSeconds > 0 ? _videoLength.ToString("c") : Loc.Get("common.unknown");
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

        private ChatUpdateOptions BuildOptions(string outputFile)
        {
            return new ChatUpdateOptions
            {
                InputFile = InputFile,
                OutputFile = outputFile,
                OutputFormat = OutputFormat,
                Compression = OutputFormat == ChatFormat.Json ? Compression : ChatCompression.None,
                EmbedMissing = EmbedMissing,
                ReplaceEmbeds = ReplaceEmbeds,
                BttvEmotes = BttvEmotes,
                FfzEmotes = FfzEmotes,
                StvEmotes = StvEmotes,
                TrimBeginning = TrimStart,
                TrimBeginningTime = TrimStart ? Math.Round(StartTime.TotalSeconds) : -1,
                TrimEnding = TrimEnd,
                TrimEndingTime = TrimEnd ? Math.Round(EndTime.TotalSeconds) : -1,
                TextTimestampFormat = TimestampStyle,
                TempFolder = _settings.Current.TempPath,
                FileCollisionCallback = file => _collision.HandleCollision(file),
            };
        }

        private TimeSpan StartTime => new(StartHour, StartMinute, StartSecond);
        private TimeSpan EndTime => new(EndHour, EndMinute, EndSecond);

        private string GetFileExtension()
        {
            return new ChatUpdateOptions
            {
                OutputFormat = OutputFormat,
                Compression = OutputFormat == ChatFormat.Json ? Compression : ChatCompression.None,
            }.FileExtension;
        }

        private (string FilterName, string Extension) GetSaveFilter()
        {
            return OutputFormat switch
            {
                ChatFormat.Html => (Loc.Get("dialogs.filter_html"), "html"),
                ChatFormat.Text => (Loc.Get("dialogs.filter_txt"), "txt"),
                _ when Compression == ChatCompression.Gzip => (Loc.Get("dialogs.filter_json_gz"), "json.gz"),
                _ => (Loc.Get("dialogs.filter_json"), "json"),
            };
        }

        private void UpdateSuggestedFileName()
        {
            SuggestedFileName = InfoLoaded
                ? BuildSuggestedFileName(GetFileExtension())
                : string.Empty;
        }

        private string BuildSuggestedFileName(string extension)
        {
            var trimStart = !TrimStart
                ? TimeSpan.FromSeconds(_chatStartSeconds)
                : StartTime;

            var trimEnd = TrimEnd ? EndTime : _videoLength;

            return FilenameService.GetFilename(
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
                string.IsNullOrEmpty(_clipperName) ? null : _clipperName,
                string.IsNullOrEmpty(_clipperId) ? null : _clipperId) + extension;
        }

        private void NotifyState()
        {
            OnPropertyChanged(nameof(CanBrowse));
            OnPropertyChanged(nameof(CanEditOptions));
            OnPropertyChanged(nameof(CanEditTrimStart));
            OnPropertyChanged(nameof(CanEditTrimEnd));
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(CanEditThirdPartyEmotes));
            BrowseCommand.NotifyCanExecuteChanged();
            LoadTypedFileCommand.NotifyCanExecuteChanged();
            UpdateCommand.NotifyCanExecuteChanged();
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
    }
}
