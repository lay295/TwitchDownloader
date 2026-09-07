using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TwitchDownloaderAvalonia.Models;
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
        private readonly AppStatus _appStatus;
        private readonly DialogService _dialogs;
        private readonly FileDialogService _fileDialogs;
        private readonly FileCollisionService _collision;
        private readonly ThumbnailService _thumbnails;
        private CancellationTokenSource? _cancellation;
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
            ThumbnailService thumbnails)
        {
            _settings = settings;
            _appStatus = appStatus;
            _dialogs = dialogs;
            _fileDialogs = fileDialogs;
            _collision = collision;
            _thumbnails = thumbnails;
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

        public string LogToggleText => IsLogExpanded ? "Hide log" : "Show log";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSuggestedFileName))]
        public partial string SuggestedFileName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Status { get; set; }

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        public partial bool IsUpdating { get; set; }

        public AppStatus AppStatus => _appStatus;

        [ObservableProperty]
        public partial bool InfoLoaded { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; private set; }

        public bool CanBrowse => !IsBusy && !IsUpdating;
        public bool CanEditOptions => InfoLoaded && !IsUpdating;
        public bool CanEditTrimStart => CanEditOptions && TrimStart;
        public bool CanEditTrimEnd => CanEditOptions && TrimEnd;
        public bool CanUpdate => InfoLoaded && !IsUpdating && !string.IsNullOrWhiteSpace(InputFile);
        public bool HasSuggestedFileName => !string.IsNullOrWhiteSpace(SuggestedFileName);
        public bool HasStreamerAvatar => StreamerAvatarBytes is { Length: > 0 };
        public bool ShowCompression => OutputFormat == ChatFormat.Json;
        public bool ShowTimestamps => OutputFormat == ChatFormat.Text;
        public bool ShowEmbedOptions => OutputFormat is ChatFormat.Json or ChatFormat.Html;
        public bool CanEditThirdPartyEmotes => CanEditOptions && ShowEmbedOptions && (EmbedMissing || ReplaceEmbeds);

        [RelayCommand(CanExecute = nameof(CanBrowse))]
        private async Task BrowseAsync()
        {
            var path = await _fileDialogs.OpenFileAsync("Open chat JSON", "JSON files", ["*.json", "*.json.gz"]);
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
            var progress = new AvaloniaTaskProgress(
                (LogLevel)_settings.Current.LogLevels,
                percent => Progress = percent,
                status => Status = status,
                AppendLog);

            var updater = new ChatUpdater(options, progress);
            try
            {
                await updater.ParseJsonAsync();
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());
                return;
            }

            IsUpdating = true;
            NotifyState();
            Status = "Updating";
            AppendLog($"Starting update: {path}");
            _cancellation = new CancellationTokenSource();

            try
            {
                await Task.Run(() => updater.UpdateAsync(_cancellation.Token));
                progress.SetStatus("Done");
                ResetAfterSuccess();
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
                IsUpdating = false;
                NotifyState();
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

        partial void OnTrimStartChanged(bool value)
        {
            UpdateSuggestedFileName();
            NotifyState();
        }

        partial void OnTrimEndChanged(bool value)
        {
            UpdateSuggestedFileName();
            NotifyState();
        }

        partial void OnStartHourChanged(int value) => UpdateSuggestedFileName();
        partial void OnStartMinuteChanged(int value) => UpdateSuggestedFileName();
        partial void OnStartSecondChanged(int value) => UpdateSuggestedFileName();
        partial void OnEndHourChanged(int value) => UpdateSuggestedFileName();
        partial void OnEndMinuteChanged(int value) => UpdateSuggestedFileName();
        partial void OnEndSecondChanged(int value) => UpdateSuggestedFileName();
        partial void OnIsBusyChanged(bool value) => NotifyState();
        partial void OnInfoLoadedChanged(bool value) => NotifyState();
        partial void OnIsUpdatingChanged(bool value)
        {
            NotifyState();
            PushAppStatus();
        }

        partial void OnStatusChanged(string value) => PushAppStatus();
        partial void OnProgressChanged(double value) => _appStatus.Progress = value;

        private async Task LoadFileAsync(string path)
        {
            var extension = Path.GetExtension(path);
            if (!extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("ERROR: Only JSON and GZip JSON chat files are supported.");
                await _dialogs.ShowErrorAsync("Unsupported file", "Please choose a .json or .json.gz chat file.");
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
                AppendLog($"Loaded {InfoTitle} from {Path.GetFileName(path)}.");
                await TryRefreshMetadataAsync();
            }
            catch (Exception ex)
            {
                InputFile = path;
                AppendLog("ERROR: " + ex.Message);
                await _dialogs.ShowErrorAsync("Unable to read chat file", ex.Message);
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyChatInfo(ChatRoot chat)
        {
            var firstComment = chat.comments?.FirstOrDefault();
            var videoCreatedAt = chat.video?.created_at == default && firstComment is not null
                ? firstComment.created_at - TimeSpan.FromSeconds(firstComment.content_offset_seconds)
                : chat.video?.created_at ?? default;

            _videoTime = _settings.Current.UtcVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
            InfoCreatedAt = videoCreatedAt == default
                ? "Unknown"
                : _videoTime.ToString(CultureInfo.CurrentCulture);

            _streamerName = chat.streamer?.name ?? "Unknown User";
            _streamerId = chat.streamer?.id.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            InfoStreamer = _streamerName;
            _title = string.IsNullOrWhiteSpace(chat.video?.title) ? "Unknown" : chat.video.title;
            InfoTitle = _title;
            _clipperName = chat.clipper?.name ?? string.Empty;
            _clipperId = chat.clipper is null ? string.Empty : chat.clipper.id.ToString(CultureInfo.InvariantCulture);
            _viewCount = chat.video?.viewCount ?? 0;
            _game = chat.video?.game
                ?? chat.video?.chapters?.FirstOrDefault()?.gameDisplayName
                ?? "Unknown Game";
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
            LengthText = _videoLength.TotalSeconds > 0 ? _videoLength.ToString("c") : "Unknown";
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
                        AppendLog("ERROR: Unable to find thumbnail: VOD expired or ID corrupt.");
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
                    AppendLog("ERROR: Unable to find thumbnail: VOD expired or ID corrupt.");
                    ThumbnailBytes = await _thumbnails.TryGetAsync(null);
                    return;
                }

                _videoLength = TimeSpan.FromSeconds(clip.durationSeconds);
                LengthText = _videoLength.ToString("c");
                _viewCount = clip.viewCount;
                _game = clip.game?.displayName ?? _game;
                if (string.IsNullOrEmpty(_clipperName))
                    _clipperName = clip.curator?.displayName ?? "Unknown User";
                if (string.IsNullOrEmpty(_clipperId))
                    _clipperId = clip.curator?.id ?? string.Empty;
                ThumbnailBytes = await _thumbnails.TryGetAsync(clip.thumbnailURL);
                StreamerAvatarBytes = await _thumbnails.TryGetAsync(clip.broadcaster?.profileImageURL);
                UpdateSuggestedFileName();
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                await _dialogs.ShowErrorAsync("Unable to get info", ex.Message);
                if (_settings.Current.VerboseErrors)
                    await _dialogs.ShowErrorAsync("Verbose error", ex.ToString());
            }
        }

        private void ResetAfterSuccess()
        {
            InputFile = string.Empty;
            _chatJson = null;
            InfoLoaded = false;
            ThumbnailBytes = null;
            StreamerAvatarBytes = null;
            InfoTitle = string.Empty;
            InfoStreamer = string.Empty;
            InfoCreatedAt = string.Empty;
            LengthText = "00:00:00";
            SuggestedFileName = string.Empty;
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
            var trimStart = TrimStart
                ? StartTime
                : TimeSpan.FromSeconds(_chatStartSeconds);
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
                _ when IsUpdating => AppStatusKind.Running,
                _ => AppStatusKind.Idle,
            };

            _appStatus.Set(kind, Status, Progress);
        }
    }
}
