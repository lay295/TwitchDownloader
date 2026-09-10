namespace TwitchDownloaderAvalonia.ViewModels
{
    public partial class VodDownloadViewModel : ViewModelBase
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
        private long _videoId;
        private DateTime _videoTime;
        private TimeSpan _vodLength;
        private int _viewCount;

        private string _game = string.Empty;
        private string _streamerId = string.Empty;
        private string _streamerName = string.Empty;
        private string _videoTitle = string.Empty;

        public VodDownloadViewModel(
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
            DownloadThreads = Math.Clamp(_settings.Current.VodDownloadThreads, 1, 20);
            TrimMode = _settings.Current.VodTrimMode;
            _suppressSave = false;
            Status = Loc.Get("status.idle");
        }

        public ObservableCollection<QualityOption> Qualities { get; } = [];

        [ObservableProperty]
        public partial string VideoUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int DownloadThreads { get; set; }

        [ObservableProperty]
        public partial QualityOption? SelectedQuality { get; set; }

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
        public partial VideoTrimMode TrimMode { get; set; }

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
                InfoCreatedAt = _videoTime.ToString(CultureInfo.CurrentCulture);

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
        public bool CanEditTrimStart => InfoLoaded && TrimStart;
        public bool CanEditTrimEnd => InfoLoaded && TrimEnd;
        public bool CanDownload => InfoLoaded && SelectedQuality is not null;
        public bool CanCancelQueued => _queued?.CanCancel == true;
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };

        [RelayCommand(CanExecute = nameof(CanGetInfo))]
        private async Task GetInfoAsync()
        {
            var videoIdMatch = IdParse.MatchVideoId(VideoUrl.Trim());
            if (videoIdMatch is not { Success: true } || !long.TryParse(videoIdMatch.ValueSpan, out var videoId))
            {
                AppendLog(Loc.Get("vod.invalid_log"));
                await _dialogs.ShowErrorAsync(Loc.Get("vod.invalid_title"), Loc.Get("vod.invalid_message"));
                return;
            }

            IsBusy = true;
            try
            {
                _videoId = videoId;
                var videoInfoTask = TwitchHelper.GetVideoInfo(videoId);
                var tokenTask = TwitchHelper.GetVideoToken(videoId, _settings.Current.OAuth);
                await Task.WhenAll(videoInfoTask, tokenTask);

                var token = tokenTask.Result.data.videoPlaybackAccessToken;
                if (token is null)
                    throw new NullReferenceException(Loc.Get("vod.deleted"));

                var playlistString = await TwitchHelper.GetVideoPlaylist(videoId, token.value, token.signature);
                if (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements"))
                    throw new NullReferenceException(Loc.Get("vod.insufficient_access"));

                var video = videoInfoTask.Result.data.video;
                var playlist = M3U8.Parse(playlistString);
                var qualities = VideoQualities.FromM3U8(playlist);

                Qualities.Clear();
                foreach (var quality in qualities)
                    Qualities.Add(new QualityOption(quality));

                SelectedQuality = Qualities.FirstOrDefault();
                _vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
                TrimHourMaximum = TrimLimits.HourMaximum(_vodLength);
                LengthText = _vodLength.ToString("c");
                _streamerName = video.owner?.displayName ?? Loc.Get("common.unknown_user");
                _streamerId = video.owner?.id ?? string.Empty;
                InfoStreamer = _streamerName;
                _videoTitle = video.title;
                InfoTitle = _videoTitle;
                var createdAt = video.createdAt;
                _videoTime = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime();
                InfoCreatedAt = _videoTime.ToString(CultureInfo.CurrentCulture);
                _viewCount = video.viewCount;
                _game = video.game?.displayName ?? Loc.Get("common.unknown_game");

                var urlTimeCodeMatch = TwitchRegex.UrlTimeCode.Match(VideoUrl);
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
                    StartHour = 0;
                    StartMinute = 0;
                    StartSecond = 0;
                }

                EndHour = (int)_vodLength.TotalHours;
                EndMinute = _vodLength.Minutes;
                EndSecond = _vodLength.Seconds;

                ThumbnailBytes = await _thumbnails.TryGetAsync(video.thumbnailURLs.FirstOrDefault());
                StreamerAvatarBytes = await _thumbnails.TryGetAsync(video.owner?.profileImageURL);

                InfoLoaded = true;
                UpdateVideoSizeEstimates();
                UpdateSuggestedFileName();
                NotifyDownloadState();
                AppendLog(Loc.Get("common.loaded_info", InfoTitle, LengthText, _streamerName));
            }
            catch (Exception ex)
            {
                AppendLog(Loc.Error(ex.Message));
                await _dialogs.ShowErrorAsync(Loc.Get("vod.get_info_failed"), ex.Message);
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
            if (!ValidateTrim())
            {
                AppendLog(Loc.Get("vod.invalid_trim"));
                return;
            }

            var extension = FilenameService.GuessVodFileExtension(SelectedQuality!.Quality.Name);
            var suggestedName = BuildSuggestedFileName(extension);

            var filterName = SelectedQuality.Quality.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase)
                ? Loc.Get("dialogs.filter_m4a")
                : Loc.Get("dialogs.filter_mp4");

            var path = await _fileDialogs.SaveFileAsync(suggestedName, filterName, extension.TrimStart('.'));
            if (string.IsNullOrWhiteSpace(path))
                return;

            var options = BuildOptions(path);
            options.CacheCleanerCallback = directories =>
            {
                if (directories.Length > 0)
                    AppendLog(Loc.Get("vod.unmanaged_caches", directories.Length));

                return [];
            };
            options.FileCollisionCallback = file => _collision.HandleCollision(file);

            var item = _queue.EnqueueVod(options, _videoTitle, ThumbnailBytes);
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

        partial void OnDownloadThreadsChanged(int value)
        {
            _settings.Current.VodDownloadThreads = Math.Clamp(value, 1, 20);
            SaveSettings();
        }

        partial void OnTrimModeChanged(VideoTrimMode value)
        {
            _settings.Current.VodTrimMode = value;
            SaveSettings();
        }

        partial void OnTrimStartChanged(bool value) => OnTrimEnabledChanged(value);
        partial void OnTrimEndChanged(bool value) => OnTrimEnabledChanged(value);
        partial void OnStartHourChanged(int value) => OnTrimTimeChanged(value);
        partial void OnStartMinuteChanged(int value) => OnTrimTimeChanged(value);
        partial void OnStartSecondChanged(int value) => OnTrimTimeChanged(value);
        partial void OnEndHourChanged(int value) => OnTrimTimeChanged(value);
        partial void OnEndMinuteChanged(int value) => OnTrimTimeChanged(value);
        partial void OnEndSecondChanged(int value) => OnTrimTimeChanged(value);
        partial void OnSelectedQualityChanged(QualityOption? value) => OnQualityOrNameChanged(value);
        partial void OnIsBusyChanged(bool value) => OnBusyOrInfoChanged(value);
        partial void OnInfoLoadedChanged(bool value) => OnBusyOrInfoChanged(value);

        private void OnTrimEnabledChanged(bool _)
        {
            UpdateVideoSizeEstimates();
            UpdateSuggestedFileName();
            NotifyDownloadState();
        }

        private void OnTrimTimeChanged(int _) => UpdateEstimatesAndSuggestedName();

        private void OnQualityOrNameChanged(QualityOption? _)
        {
            UpdateSuggestedFileName();
            DownloadCommand.NotifyCanExecuteChanged();
        }

        private void OnBusyOrInfoChanged(bool _) => NotifyDownloadState();

        private VideoDownloadOptions BuildOptions(string filename)
        {
            return new VideoDownloadOptions
            {
                DownloadThreads = DownloadThreads,
                ThrottleKib = _settings.Current.DownloadThrottleEnabled ? _settings.Current.MaximumBandwidthKib : -1,
                Filename = filename,
                Oauth = _settings.Current.OAuth,
                Quality = SelectedQuality!.Quality.ToString(),
                Id = _videoId,
                TrimBeginning = TrimStart,
                TrimBeginningTime = StartTime,
                TrimEnding = TrimEnd,
                TrimEndingTime = EndTime,
                FfmpegPath = _ffmpeg.ResolvedPath,
                TempFolder = _settings.Current.TempPath,
                TrimMode = TrimMode,
            };
        }

        private TimeSpan StartTime => new(StartHour, StartMinute, StartSecond);
        private TimeSpan EndTime => new(EndHour, EndMinute, EndSecond);

        private bool ValidateTrim()
        {
            if (!TrimStart)
                return true;

            if (_vodLength > TimeSpan.Zero && StartTime.TotalSeconds >= _vodLength.TotalSeconds)
                return false;

            return !TrimEnd || EndTime.TotalSeconds >= StartTime.TotalSeconds;
        }

        private void UpdateVideoSizeEstimates()
        {
            var trimStart = TrimStart ? StartTime : TimeSpan.Zero;
            var trimEnd = TrimEnd ? EndTime : _vodLength;

            foreach (var item in Qualities)
            {
                var sizeInBytes = VideoSizeEstimator.EstimateVideoSize(item.Quality.BitRate, trimStart, trimEnd);
                item.DisplayName = QualityLabels.WithSize(item.Quality.Name, sizeInBytes);
            }
        }

        private void UpdateEstimatesAndSuggestedName()
        {
            UpdateVideoSizeEstimates();
            UpdateSuggestedFileName();
        }

        private void UpdateSuggestedFileName()
        {
            if (!InfoLoaded || SelectedQuality is null)
            {
                SuggestedFileName = string.Empty;
                return;
            }

            var extension = FilenameService.GuessVodFileExtension(SelectedQuality.Quality.Name);
            SuggestedFileName = BuildSuggestedFileName(extension);
        }

        private string BuildSuggestedFileName(string extension)
        {
            return FilenameService.GetFilename(
                _settings.Current.TemplateVod,
                _videoTitle,
                _videoId.ToString(),
                _videoTime,
                _streamerName,
                _streamerId,
                TrimStart ? StartTime : TimeSpan.Zero,
                TrimEnd ? EndTime : _vodLength,
                _vodLength,
                _viewCount,
                _game) + extension;
        }

        private void NotifyDownloadState()
        {
            OnPropertyChanged(nameof(CanGetInfo));
            OnPropertyChanged(nameof(CanEditTrimStart));
            OnPropertyChanged(nameof(CanEditTrimEnd));
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

        private void SaveSettings()
        {
            if (_suppressSave)
                return;

            _settings.Save();
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
