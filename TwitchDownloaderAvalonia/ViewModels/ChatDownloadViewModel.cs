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
    public partial class ChatDownloadViewModel : ViewModelBase
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _appStatus;
        private readonly DialogService _dialogs;
        private readonly FileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private CancellationTokenSource? _cancellation;
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
            ThumbnailService thumbnails)
        {
            _settings = settings;
            _appStatus = appStatus;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _collision = collision;
            _thumbnails = thumbnails;
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
        public bool CanEditTrimStart => CanEditOptions && TrimStart;
        public bool CanEditTrimEnd => CanEditOptions && TrimEnd;
        public bool CanDownload => InfoLoaded && !IsDownloading;
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
                AppendLog("ERROR: Invalid VOD or clip link.");
                await _dialogs.ShowErrorAsync("Unable to parse link", "Please enter a valid Twitch VOD or clip link.");
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
                AppendLog($"Loaded {InfoTitle} ({LengthText}) from {_streamerName}.");
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                await _dialogs.ShowErrorAsync("Unable to get chat info", ex.Message);
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
            var extension = GetFileExtension();
            var suggestedName = BuildSuggestedFileName(extension);
            var (filterName, filterExtension) = GetSaveFilter();
            var path = await _fileDialogs.SaveFileAsync(suggestedName, filterName, filterExtension);
            if (string.IsNullOrWhiteSpace(path))
                return;

            var options = BuildOptions(path);
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
                var downloader = new ChatDownloader(options, progress);
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

        partial void OnTrimStartChanged(bool value)
        {
            UpdateSuggestedFileName();
            NotifyDownloadState();
        }

        partial void OnTrimEndChanged(bool value)
        {
            UpdateSuggestedFileName();
            NotifyDownloadState();
        }

        partial void OnStartHourChanged(int value) => UpdateSuggestedFileName();
        partial void OnStartMinuteChanged(int value) => UpdateSuggestedFileName();
        partial void OnStartSecondChanged(int value) => UpdateSuggestedFileName();
        partial void OnEndHourChanged(int value) => UpdateSuggestedFileName();
        partial void OnEndMinuteChanged(int value) => UpdateSuggestedFileName();
        partial void OnEndSecondChanged(int value) => UpdateSuggestedFileName();
        partial void OnIsBusyChanged(bool value) => NotifyDownloadState();
        partial void OnInfoLoadedChanged(bool value) => NotifyDownloadState();
        partial void OnIsDownloadingChanged(bool value)
        {
            NotifyDownloadState();
            PushAppStatus();
        }

        partial void OnStatusChanged(string value) => PushAppStatus();
        partial void OnProgressChanged(double value) => _appStatus.Progress = value;

        private async Task LoadVideoInfoAsync()
        {
            if (!long.TryParse(_downloadId, out var videoId))
                throw new InvalidOperationException("Invalid VOD ID.");

            var videoInfo = await TwitchHelper.GetVideoInfo(videoId);
            var video = videoInfo.data.video
                ?? throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");

            _vodLength = TimeSpan.FromSeconds(video.lengthSeconds);
            ApplyInfo(
                video.title,
                video.owner?.displayName ?? "Unknown User",
                video.owner?.id ?? string.Empty,
                video.createdAt,
                video.viewCount,
                video.game?.displayName ?? "Unknown Game",
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
            var clip = clipInfo.data.clip
                ?? throw new NullReferenceException("Invalid clip, deleted possibly?");

            _vodLength = TimeSpan.FromSeconds(clip.durationSeconds);
            ApplyInfo(
                clip.title,
                clip.broadcaster?.displayName ?? "Unknown User",
                clip.broadcaster?.id ?? string.Empty,
                clip.createdAt,
                clip.viewCount,
                clip.game?.displayName ?? "Unknown Game",
                clip.curator?.displayName ?? "Unknown User",
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
                ChatFormat.Html => ("HTML files", "html"),
                ChatFormat.Text => ("TXT files", "txt"),
                _ when Compression == ChatCompression.Gzip => ("GZip JSON files", "json.gz"),
                _ => ("JSON files", "json"),
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
