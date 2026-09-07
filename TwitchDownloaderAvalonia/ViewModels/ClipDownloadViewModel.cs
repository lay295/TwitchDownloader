using System.Collections.ObjectModel;
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
    public partial class ClipDownloadViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _appStatus;
        private readonly FfmpegService _ffmpeg;
        private readonly DialogService _dialogs;
        private readonly FileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private CancellationTokenSource? _cancellation;
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
            ThumbnailService thumbnails)
        {
            _settings = settings;
            _appStatus = appStatus;
            _ffmpeg = ffmpeg;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _collision = collision;
            _thumbnails = thumbnails;
            EncodeMetadata = _settings.Current.EncodeClipMetadata;
            Status = "Idle";
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

        public string LogToggleText => IsLogExpanded ? "Hide log" : "Show log";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSuggestedFileName))]
        public partial string SuggestedFileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Status { get; set; }

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        public partial bool IsDownloading { get; set; }

        public AppStatus AppStatus => _appStatus;

        [ObservableProperty]
        public partial bool InfoLoaded { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; private set; }

        public bool CanGetInfo => !IsBusy && !IsDownloading;
        public bool CanEditOptions => InfoLoaded && !IsDownloading;
        public bool CanDownload => InfoLoaded && !IsDownloading && SelectedQuality is not null;
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };

        [RelayCommand(CanExecute = nameof(CanGetInfo))]
        private async Task GetInfoAsync()
        {
            var clipIdMatch = IdParse.MatchClipId(ClipUrl.Trim());
            if (clipIdMatch is not { Success: true })
            {
                AppendLog("ERROR: Invalid clip link or ID.");
                await _dialogs.ShowErrorAsync("Invalid clip", "Please enter a valid Twitch clip link or ID.");
                return;
            }

            IsBusy = true;
            try
            {
                _clipId = clipIdMatch.Value;
                var clipRenderStatus = await TwitchHelper.GetShareClipRenderStatus(_clipId);
                var clip = clipRenderStatus.data.clip
                    ?? throw new NullReferenceException("Invalid clip, deleted possibly?");

                Qualities.Clear();
                foreach (var quality in VideoQualities.FromClip(clip).Qualities)
                    Qualities.Add(new ClipQualityOption(quality));

                SelectedQuality = Qualities.FirstOrDefault();
                _clipLength = TimeSpan.FromSeconds(clip.durationSeconds);
                LengthText = _clipLength.ToString("c");
                _streamerName = clip.broadcaster?.displayName ?? "Unknown User";
                _streamerId = clip.broadcaster?.id ?? string.Empty;
                InfoStreamer = _streamerName;
                _clipperName = clip.curator?.displayName ?? "Unknown User";
                _clipperId = clip.curator?.id ?? string.Empty;
                _clipTitle = clip.title;
                InfoTitle = _clipTitle;
                var createdAt = clip.createdAt;
                _clipTime = _settings.Current.UtcVideoTime ? createdAt : createdAt.ToLocalTime();
                InfoCreatedAt = _clipTime.ToString(CultureInfo.CurrentCulture);
                _viewCount = clip.viewCount;
                _game = clip.game?.displayName ?? "Unknown Game";

                ThumbnailBytes = await _thumbnails.TryGetAsync(clip.thumbnailURL);
                StreamerAvatarBytes = await _thumbnails.TryGetAsync(clip.broadcaster?.profileImageURL);

                InfoLoaded = true;
                UpdateVideoSizeEstimates();
                UpdateSuggestedFileName();
                NotifyDownloadState();
                AppendLog($"Loaded {InfoTitle} ({LengthText}) from {_streamerName}.");
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                await _dialogs.ShowErrorAsync("Unable to get clip info", ex.Message);
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
            var suggestedName = BuildSuggestedFileName();
            var path = await _fileDialogs.SaveFileAsync(suggestedName, "MP4 files", "mp4");
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

            IsDownloading = true;
            NotifyDownloadState();
            Status = "Downloading";
            AppendLog($"Starting download: {path}");
            _cancellation = new CancellationTokenSource();
            var progress = new AvaloniaTaskProgress(
                (LogLevel)_settings.Current.LogLevels,
                percent => Progress = percent,
                status => Status = status,
                AppendLog);

            try
            {
                var downloader = new ClipDownloader(options, progress);
                await Task.Run(() => downloader.DownloadAsync(_cancellation.Token));
                progress.SetStatus("Done");
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellation.IsCancellationRequested)
            {
                progress.SetStatus("Canceled");
            }
            catch (Exception ex)
            {
                progress.SetStatus("Error");
                AppendLog("ERROR: " + ex.Message);
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());
            }
            finally
            {
                progress.ReportProgress(0);
                _cancellation.Dispose();
                _cancellation = null;
                IsDownloading = false;
                NotifyDownloadState();
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Status = "Canceling";
            try
            {
                _cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
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
            _settings.Save();
        }

        partial void OnSelectedQualityChanged(ClipQualityOption? value)
        {
            UpdateSuggestedFileName();
            DownloadCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value) => NotifyDownloadState();
        partial void OnInfoLoadedChanged(bool value) => NotifyDownloadState();
        partial void OnIsDownloadingChanged(bool value)
        {
            NotifyDownloadState();
            PushAppStatus();
        }

        partial void OnStatusChanged(string value) => PushAppStatus();
        partial void OnProgressChanged(double value) => _appStatus.Progress = value;

        private void UpdateVideoSizeEstimates()
        {
            foreach (var item in Qualities)
            {
                var sizeInBytes = VideoSizeEstimator.EstimateVideoSize(item.Quality.BitRate, _clipLength);
                item.DisplayName = sizeInBytes != 0
                    ? $"{item.Quality.Name} - {VideoSizeEstimator.StringifyByteCount(sizeInBytes)}"
                    : item.Quality.Name;
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
        }

        public void AppendLog(string message)
        {
            var builder = new StringBuilder(LogText);
            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(message);
            LogText = builder.ToString();
        }

        private void PushAppStatus()
        {
            var kind = Status switch
            {
                "Canceling" => AppStatusKind.Canceling,
                "Error" => AppStatusKind.Error,
                _ when IsDownloading => AppStatusKind.Running,
                _ => AppStatusKind.Idle,
            };

            _appStatus.Set(kind, Status, Progress);
        }
    }
}
