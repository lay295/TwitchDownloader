using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;

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
            DownloadThreads = Math.Clamp(_settings.Current.VodDownloadThreads, 1, 20);
            Oauth = _settings.Current.OAuth;
            TrimMode = _settings.Current.VodTrimMode;
            Status = "Idle";
        }

        public ObservableCollection<QualityOption> Qualities { get; } = [];

        [ObservableProperty]
        public partial string VideoUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Oauth { get; set; }

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

        public string LogToggleText => IsLogExpanded ? "Hide log" : "Show log";

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
                AppendLog("ERROR: Invalid VOD link or ID.");
                await _dialogs.ShowErrorAsync("Invalid VOD", "Please enter a valid Twitch VOD link or ID.");
                return;
            }

            IsBusy = true;
            try
            {
                _videoId = videoId;
                var videoInfoTask = TwitchHelper.GetVideoInfo(videoId);
                var tokenTask = TwitchHelper.GetVideoToken(videoId, Oauth);
                await Task.WhenAll(videoInfoTask, tokenTask);

                var token = tokenTask.Result.data.videoPlaybackAccessToken;
                if (token is null)
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");

                var playlistString = await TwitchHelper.GetVideoPlaylist(videoId, token.value, token.signature);
                if (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements"))
                    throw new NullReferenceException("Insufficient access. The VOD may be subscriber-only; try providing an OAuth token.");

                var video = videoInfoTask.Result.data.video;
                var playlist = M3U8.Parse(playlistString);
                var qualities = VideoQualities.FromM3U8(playlist);

                Qualities.Clear();
                foreach (var quality in qualities)
                    Qualities.Add(new QualityOption(quality));

                SelectedQuality = Qualities.FirstOrDefault();
                _vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
                LengthText = _vodLength.ToString("c");
                _streamerName = video.owner?.displayName ?? "Unknown User";
                _streamerId = video.owner?.id ?? string.Empty;
                InfoStreamer = _streamerName;
                _videoTitle = video.title;
                InfoTitle = _videoTitle;
                var createdAt = video.createdAt;
                _videoTime = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime();
                InfoCreatedAt = _videoTime.ToString(CultureInfo.CurrentCulture);
                _viewCount = video.viewCount;
                _game = video.game?.displayName ?? "Unknown Game";

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
                AppendLog($"Loaded {InfoTitle} ({LengthText}) from {_streamerName}.");
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                await _dialogs.ShowErrorAsync("Unable to get video info", ex.Message);
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());
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
                AppendLog("ERROR: Invalid trim inputs.");
                return;
            }

            var extension = FilenameService.GuessVodFileExtension(SelectedQuality!.Quality.Name);
            var suggestedName = BuildSuggestedFileName(extension);

            var filterName = SelectedQuality.Quality.Name.Contains("Audio", StringComparison.OrdinalIgnoreCase)
                ? "M4A files"
                : "MP4 files";

            var path = await _fileDialogs.SaveFileAsync(suggestedName, filterName, extension.TrimStart('.'));
            if (string.IsNullOrWhiteSpace(path))
                return;

            var options = BuildOptions(path);
            options.CacheCleanerCallback = directories =>
            {
                if (directories.Length > 0)
                    AppendLog($"{directories.Length} unmanaged video caches were found and can be deleted later from the cache folder.");

                return [];
            };
            options.FileCollisionCallback = file => _collision.HandleCollision(file);

            var item = _queue.EnqueueVod(options, _videoTitle, ThumbnailBytes);
            TrackQueued(item);
            AppendLog($"Added to queue: {path}");
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

        partial void OnOauthChanged(string value)
        {
            _settings.Current.OAuth = value;
            _settings.Save();
        }

        partial void OnDownloadThreadsChanged(int value)
        {
            _settings.Current.VodDownloadThreads = Math.Clamp(value, 1, 20);
            _settings.Save();
        }

        partial void OnTrimModeChanged(VideoTrimMode value)
        {
            _settings.Current.VodTrimMode = value;
            _settings.Save();
        }

        partial void OnTrimStartChanged(bool value)
        {
            UpdateVideoSizeEstimates();
            UpdateSuggestedFileName();
            NotifyDownloadState();
        }

        partial void OnTrimEndChanged(bool value)
        {
            UpdateVideoSizeEstimates();
            UpdateSuggestedFileName();
            NotifyDownloadState();
        }

        partial void OnStartHourChanged(int value) => UpdateEstimatesAndSuggestedName();
        partial void OnStartMinuteChanged(int value) => UpdateEstimatesAndSuggestedName();
        partial void OnStartSecondChanged(int value) => UpdateEstimatesAndSuggestedName();
        partial void OnEndHourChanged(int value) => UpdateEstimatesAndSuggestedName();
        partial void OnEndMinuteChanged(int value) => UpdateEstimatesAndSuggestedName();
        partial void OnEndSecondChanged(int value) => UpdateEstimatesAndSuggestedName();
        partial void OnSelectedQualityChanged(QualityOption? value)
        {
            UpdateSuggestedFileName();
            DownloadCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value) => NotifyDownloadState();
        partial void OnInfoLoadedChanged(bool value) => NotifyDownloadState();

        private VideoDownloadOptions BuildOptions(string filename)
        {
            return new VideoDownloadOptions
            {
                DownloadThreads = DownloadThreads,
                ThrottleKib = _settings.Current.DownloadThrottleEnabled ? _settings.Current.MaximumBandwidthKib : -1,
                Filename = filename,
                Oauth = Oauth,
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
                item.DisplayName = sizeInBytes != 0
                    ? $"{item.Quality.Name} - {VideoSizeEstimator.StringifyByteCount(sizeInBytes)}"
                    : item.Quality.Name;
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
