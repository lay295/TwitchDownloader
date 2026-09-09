using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class ChatDownloadViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly DialogService _dialogs;
        private readonly FileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private readonly QueueService _queue;
        private QueueItemViewModel? _queued;
        private string _downloadId = string.Empty;
        private bool _isClip;
        private DateTime _videoTime;
        private TimeSpan _vodLength;
        private int _viewCount;
        private string _game = string.Empty;
        private string _streamerId = string.Empty;
        private string _streamerName = string.Empty;
        private string _clipperName = string.Empty;
        private string _clipperId = string.Empty;
        private string _title = string.Empty;

        public ChatDownloadViewModel(
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
            DownloadFormat = _settings.Current.ChatDownloadFormat;
            Compression = _settings.Current.ChatJsonCompression;
            TimestampStyle = _settings.Current.ChatTextTimestampStyle;
            EmbedImages = _settings.Current.ChatEmbedEmotes;
            BttvEmotes = _settings.Current.BttvEmotes;
            FfzEmotes = _settings.Current.FfzEmotes;
            StvEmotes = _settings.Current.StvEmotes;
            DownloadThreads = Math.Clamp(_settings.Current.ChatDownloadThreads, 1, 20);
            Status = "Idle";
        }

        [ObservableProperty]
        public partial string SourceUrl { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCompression))]
        [NotifyPropertyChangedFor(nameof(ShowTimestamps))]
        [NotifyPropertyChangedFor(nameof(ShowEmbedOptions))]
        [NotifyPropertyChangedFor(nameof(CanEditThirdPartyEmotes))]
        public partial ChatFormat DownloadFormat { get; set; }

        [ObservableProperty]
        public partial ChatCompression Compression { get; set; }

        [ObservableProperty]
        public partial TimestampFormat TimestampStyle { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEditThirdPartyEmotes))]
        public partial bool EmbedImages { get; set; }

        [ObservableProperty]
        public partial bool BttvEmotes { get; set; }

        [ObservableProperty]
        public partial bool FfzEmotes { get; set; }

        [ObservableProperty]
        public partial bool StvEmotes { get; set; }

        [ObservableProperty]
        public partial int DownloadThreads { get; set; }

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

        public bool CanGetInfo => !IsBusy;
        public bool CanEditOptions => InfoLoaded;
        public bool CanEditTrimStart => CanEditOptions && TrimStart;
        public bool CanEditTrimEnd => CanEditOptions && TrimEnd;
        public bool CanDownload => InfoLoaded;
        public bool CanCancelQueued => _queued?.CanCancel == true;
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };
        public bool ShowCompression => DownloadFormat == ChatFormat.Json;
        public bool ShowTimestamps => DownloadFormat == ChatFormat.Text;
        public bool ShowEmbedOptions => DownloadFormat is ChatFormat.Json or ChatFormat.Html;
        public bool CanEditThirdPartyEmotes => CanEditOptions && EmbedImages && ShowEmbedOptions;

        [RelayCommand(CanExecute = nameof(CanGetInfo))]
        private async Task GetInfoAsync()
        {
            var idMatch = IdParse.MatchVideoOrClipId(SourceUrl.Trim());
            if (idMatch is not { Success: true })
            {
                AppendLog(Loc.Get("chat.invalid_log"));
                await _dialogs.ShowErrorAsync(Loc.Get("chat.invalid_title"), Loc.Get("chat.invalid_message"));
                return;
            }

            IsBusy = true;
            try
            {
                _downloadId = idMatch.Value;
                _isClip = !_downloadId.All(char.IsDigit);

                if (_isClip)
                    await LoadClipInfoAsync();
                else
                    await LoadVideoInfoAsync();

                InfoLoaded = true;
                UpdateSuggestedFileName();
                NotifyDownloadState();

                AppendLog(Loc.Get("common.loaded_info", InfoTitle, LengthText, _streamerName));
            }
            catch (Exception ex)
            {
                AppendLog(Loc.Error(ex.Message));
                await _dialogs.ShowErrorAsync(Loc.Get("chat.get_info_failed"), ex.Message);
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadAsync()
        {
            var extension = GetFileExtension();
            var suggestedName = BuildSuggestedFileName(extension);
            var (filterName, filterExtension) = GetSaveFilter();
            var path = await _fileDialogs.SaveFileAsync(suggestedName, filterName, filterExtension);
            if (string.IsNullOrWhiteSpace(path))
                return;

            var options = BuildOptions(path);
            var item = _queue.EnqueueChat(options, _title, ThumbnailBytes);
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

        partial void OnDownloadFormatChanged(ChatFormat value)
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

        partial void OnEmbedImagesChanged(bool value)
        {
            _settings.Current.ChatEmbedEmotes = value;
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

        partial void OnDownloadThreadsChanged(int value)
        {
            _settings.Current.ChatDownloadThreads = Math.Clamp(value, 1, 20);
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
            NotifyDownloadState();
        }

        private void OnTrimTimeChanged(int _) => UpdateSuggestedFileName();
        private void OnBusyOrInfoChanged(bool _) => NotifyDownloadState();

        private async Task LoadVideoInfoAsync()
        {
            if (!long.TryParse(_downloadId, out var videoId))
                throw new InvalidOperationException(Loc.Get("chat.invalid_vod_id"));

            var videoInfo = await TwitchHelper.GetVideoInfo(videoId);
            var video = videoInfo.data.video ?? throw new NullReferenceException(Loc.Get("vod.deleted"));

            _vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
            ApplyInfo(
                video.title,
                video.owner?.displayName ?? Loc.Get("common.unknown_user"),
                video.owner?.id ?? string.Empty,
                video.createdAt,
                video.viewCount,
                video.game?.displayName ?? Loc.Get("common.unknown_game"),
                clipperName: string.Empty,
                clipperId: string.Empty);

            var urlTimeCodeMatch = TwitchRegex.UrlTimeCode.Match(SourceUrl);
            if (urlTimeCodeMatch.Success)
            {
                var time = UrlTimeCode.Parse(urlTimeCodeMatch.ValueSpan);
                TrimStart = true;
                StartHour = (int)time.TotalHours;
                StartMinute = time.Minutes;
                StartSecond = time.Seconds;
            }
            else
            {
                TrimStart = false;
                StartHour = 0;
                StartMinute = 0;
                StartSecond = 0;
            }

            TrimHourMaximum = _vodLength > TimeSpan.Zero ? (int)_vodLength.TotalHours : 48;
            EndHour = (int)_vodLength.TotalHours;
            EndMinute = _vodLength.Minutes;
            EndSecond = _vodLength.Seconds;

            ThumbnailBytes = await _thumbnails.TryGetAsync(video.thumbnailURLs.FirstOrDefault());
            StreamerAvatarBytes = await _thumbnails.TryGetAsync(video.owner?.profileImageURL);
        }

        private async Task LoadClipInfoAsync()
        {
            var clipInfo = await TwitchHelper.GetClipInfo(_downloadId);
            var clip = clipInfo.data.clip ?? throw new NullReferenceException(Loc.Get("clip.deleted"));

            _vodLength = TimeSpan.FromSeconds(clip.durationSeconds);
            ApplyInfo(
                clip.title,
                clip.broadcaster?.displayName ?? Loc.Get("common.unknown_user"),
                clip.broadcaster?.id ?? string.Empty,
                clip.createdAt,
                clip.viewCount,
                clip.game?.displayName ?? Loc.Get("common.unknown_game"),
                clip.curator?.displayName ?? Loc.Get("common.unknown_user"),
                clip.curator?.id ?? string.Empty);

            TrimStart = false;
            TrimEnd = false;
            TrimHourMaximum = 0;
            StartHour = 0;
            StartMinute = 0;
            StartSecond = 0;
            EndHour = 0;
            EndMinute = _vodLength.Minutes;
            EndSecond = _vodLength.Seconds;

            ThumbnailBytes = await _thumbnails.TryGetAsync(clip.thumbnailURL);
            StreamerAvatarBytes = await _thumbnails.TryGetAsync(clip.broadcaster?.profileImageURL);
        }

        private void ApplyInfo(
            string title,
            string streamerName,
            string streamerId,
            DateTime createdAt,
            int viewCount,
            string game,
            string clipperName,
            string clipperId)
        {
            _title = title;
            InfoTitle = title;
            _streamerName = streamerName;
            _streamerId = streamerId;
            InfoStreamer = streamerName;
            _clipperName = clipperName;
            _clipperId = clipperId;
            _videoTime = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime();
            InfoCreatedAt = _videoTime.ToString(CultureInfo.CurrentCulture);
            _viewCount = viewCount;
            _game = game;
            LengthText = _vodLength.ToString("c");
        }

        private ChatDownloadOptions BuildOptions(string filename)
        {
            return new ChatDownloadOptions
            {
                DownloadFormat = DownloadFormat,
                Compression = DownloadFormat == ChatFormat.Json ? Compression : ChatCompression.None,
                TimeFormat = TimestampStyle,
                TrimBeginning = TrimStart,
                TrimBeginningTime = StartTime.TotalSeconds,
                TrimEnding = TrimEnd,
                TrimEndingTime = EndTime.TotalSeconds,
                EmbedData = EmbedImages,
                BttvEmotes = BttvEmotes,
                FfzEmotes = FfzEmotes,
                StvEmotes = StvEmotes,
                Id = _downloadId,
                Filename = filename,
                DownloadThreads = DownloadThreads,
                TempFolder = _settings.Current.TempPath,
                FileCollisionCallback = file => _collision.HandleCollision(file),
            };
        }

        private TimeSpan StartTime => new(StartHour, StartMinute, StartSecond);
        private TimeSpan EndTime => new(EndHour, EndMinute, EndSecond);

        private string GetFileExtension()
        {
            return new ChatDownloadOptions
            {
                DownloadFormat = DownloadFormat,
                Compression = DownloadFormat == ChatFormat.Json ? Compression : ChatCompression.None,
            }.FileExtension;
        }

        private (string FilterName, string Extension) GetSaveFilter()
        {
            return DownloadFormat switch
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
            return FilenameService.GetFilename(
                _settings.Current.TemplateChat,
                _title,
                _downloadId,
                _videoTime,
                _streamerName,
                _streamerId,
                TrimStart ? StartTime : TimeSpan.Zero,
                TrimEnd ? EndTime : _vodLength,
                _vodLength,
                _viewCount,
                _game,
                string.IsNullOrEmpty(_clipperName) ? null : _clipperName,
                string.IsNullOrEmpty(_clipperId) ? null : _clipperId) + extension;
        }

        private void NotifyDownloadState()
        {
            OnPropertyChanged(nameof(CanGetInfo));
            OnPropertyChanged(nameof(CanEditOptions));
            OnPropertyChanged(nameof(CanEditTrimStart));
            OnPropertyChanged(nameof(CanEditTrimEnd));
            OnPropertyChanged(nameof(CanDownload));
            OnPropertyChanged(nameof(CanEditThirdPartyEmotes));
            GetInfoCommand.NotifyCanExecuteChanged();
            DownloadCommand.NotifyCanExecuteChanged();
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
            NotifyDownloadState();
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
                NotifyDownloadState();
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
