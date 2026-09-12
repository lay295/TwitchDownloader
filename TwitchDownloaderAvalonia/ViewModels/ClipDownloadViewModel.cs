namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class ClipDownloadViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly FfmpegService _ffmpeg;
        private readonly DialogService _dialogs;
        private readonly FileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private readonly QueueService _queue;

        private readonly bool _suppressSave;

        private QueueItemViewModel? _queued;
        private string _clipId = string.Empty;
        private DateTime _clipTime;
        private TimeSpan _clipLength;
        private int _viewCount;

        private string _game = string.Empty;
        private string _streamerId = string.Empty;
        private string _streamerName = string.Empty;
        private string _clipperName = string.Empty;
        private string _clipperId = string.Empty;
        private string _clipTitle = string.Empty;

        public ClipDownloadViewModel(
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
            _suppressSave = true;
            EncodeMetadata = _settings.Current.EncodeClipMetadata;
            _suppressSave = false;
            Status = Loc.Get("status.idle");
        }

        public ObservableCollection<ClipQualityOption> Qualities { get; } = [];

        [ObservableProperty]
        public partial string ClipUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ClipQualityOption? SelectedQuality { get; set; }

        [ObservableProperty]
        public partial bool EncodeMetadata { get; set; }

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
                InfoCreatedAt = _clipTime.ToString(CultureInfo.CurrentCulture);

            Notify(nameof(LogToggleText), nameof(SuggestedFileDisplay));
            UpdateVideoSizeEstimates();
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

        public bool CanGetInfo => !IsBusy;
        public bool CanEditOptions => InfoLoaded;
        public bool CanDownload => InfoLoaded && SelectedQuality is not null;
        public bool CanCancelQueued => _queued?.CanCancel == true;
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };

        [RelayCommand(CanExecute = nameof(CanGetInfo))]
        private async Task GetInfoAsync()
        {
            var clipIdMatch = IdParse.MatchClipId(ClipUrl.Trim());
            if (clipIdMatch is not { Success: true })
            {
                AppendLog(Loc.Get("clip.invalid_log"));
                await _dialogs.ShowErrorAsync(Loc.Get("clip.invalid_title"), Loc.Get("clip.invalid_message"));
                return;
            }

            IsBusy = true;
            try
            {
                _clipId = clipIdMatch.Value;
                var clipRenderStatus = await TwitchHelper.GetShareClipRenderStatus(_clipId);
                var clip = clipRenderStatus.data.clip
                    ?? throw new NullReferenceException(Loc.Get("clip.deleted"));

                Qualities.Clear();
                foreach (var quality in VideoQualities.FromClip(clip).Qualities)
                    Qualities.Add(new ClipQualityOption(quality));

                SelectedQuality = Qualities.FirstOrDefault();
                _clipLength = TimeSpan.FromSeconds(clip.durationSeconds);
                LengthText = _clipLength.ToString("c");
                _streamerName = clip.broadcaster?.displayName ?? Loc.Get("common.unknown_user");
                _streamerId = clip.broadcaster?.id ?? string.Empty;
                InfoStreamer = _streamerName;
                _clipperName = clip.curator?.displayName ?? Loc.Get("common.unknown_user");
                _clipperId = clip.curator?.id ?? string.Empty;
                _clipTitle = clip.title;
                InfoTitle = _clipTitle;
                var createdAt = clip.createdAt;
                _clipTime = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime();
                InfoCreatedAt = _clipTime.ToString(CultureInfo.CurrentCulture);
                _viewCount = clip.viewCount;
                _game = clip.game?.displayName ?? Loc.Get("common.unknown_game");

                ThumbnailBytes = await _thumbnails.TryGetAsync(clip.thumbnailURL);
                StreamerAvatarBytes = await _thumbnails.TryGetAsync(clip.broadcaster?.profileImageURL);

                InfoLoaded = true;
                UpdateVideoSizeEstimates();
                UpdateSuggestedFileName();
                NotifyDownloadState();
                AppendLog(Loc.Get("common.loaded_info", InfoTitle, LengthText, _streamerName));
            }
            catch (Exception ex)
            {
                AppendLog(Loc.Error(ex.Message));
                await _dialogs.ShowErrorAsync(Loc.Get("clip.get_info_failed"), ex.Message);
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
            var suggestedName = BuildSuggestedFileName();
            var path = await _fileDialogs.SaveFileAsync(suggestedName, Loc.Get("dialogs.filter_mp4"), "mp4");
            if (string.IsNullOrWhiteSpace(path))
                return;

            var options = new ClipDownloadOptions
            {
                Filename = path,
                Id = _clipId,
                Quality = SelectedQuality!.Quality.ToString(),
                ThrottleKib = _settings.Current.DownloadThrottleEnabled ? _settings.Current.MaximumBandwidthKib : -1,
                TempFolder = _settings.Current.TempPath,
                EncodeMetadata = EncodeMetadata,
                FfmpegPath = _ffmpeg.ResolvedPath,
                FileCollisionCallback = file => _collision.HandleCollision(file),
            };

            var item = _queue.EnqueueClip(options, _clipTitle, ThumbnailBytes);
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

        partial void OnEncodeMetadataChanged(bool value)
        {
            _settings.Current.EncodeClipMetadata = value;
            if (!_suppressSave)
                _settings.Save();
        }

        partial void OnSelectedQualityChanged(ClipQualityOption? value) => OnQualityOrNameChanged(value);
        partial void OnIsBusyChanged(bool value) => OnBusyOrInfoChanged(value);
        partial void OnInfoLoadedChanged(bool value) => OnBusyOrInfoChanged(value);

        private void OnQualityOrNameChanged(ClipQualityOption? _)
        {
            UpdateSuggestedFileName();
            DownloadCommand.NotifyCanExecuteChanged();
        }

        private void OnBusyOrInfoChanged(bool _) => NotifyDownloadState();

        private void UpdateVideoSizeEstimates()
        {
            foreach (var item in Qualities)
            {
                var sizeInBytes = VideoSizeEstimator.EstimateVideoSize(item.Quality.BitRate, _clipLength);
                item.DisplayName = QualityLabels.WithSize(item.Quality.Name, sizeInBytes);
            }
        }

        private void UpdateSuggestedFileName()
        {
            SuggestedFileName = InfoLoaded && SelectedQuality is not null
                ? BuildSuggestedFileName()
                : string.Empty;
        }

        private string BuildSuggestedFileName()
        {
            return FilenameService.GetFilename(
                _settings.Current.TemplateClip,
                _clipTitle,
                _clipId,
                _clipTime,
                _streamerName,
                _streamerId,
                TimeSpan.Zero,
                _clipLength,
                _clipLength,
                _viewCount,
                _game,
                _clipperName,
                _clipperId) + ".mp4";
        }

        private void NotifyDownloadState()
        {
            OnPropertyChanged(nameof(CanGetInfo));
            OnPropertyChanged(nameof(CanEditOptions));
            OnPropertyChanged(nameof(CanDownload));
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

        private void AppendLog(string message)
        {
            var builder = new StringBuilder(LogText);
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(message);
            LogText = builder.ToString();
        }
    }
}
