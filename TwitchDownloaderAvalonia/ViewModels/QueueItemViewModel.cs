using System.Runtime.InteropServices;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public sealed partial class QueueItemViewModel : ViewModelBase
    {
        private readonly object _options;
        private readonly LogLevel _logLevel;

        private CancellationTokenSource _tokenSource = new();
        private QueueService? _queue;
        private DialogService? _dialogs;
        private AppStatus? _appStatus;
        private bool _hasLiveProgressStatus;

        private QueueItemViewModel(
            QueueTaskKind kind,
            object options,
            string title,
            byte[]? thumbnailBytes,
            string sourceId,
            LogLevel logLevel,
            QueueItemViewModel? dependantTask)
        {
            Kind = kind;
            _options = options;
            Title = title;
            ThumbnailBytes = thumbnailBytes;
            SourceId = sourceId;
            _logLevel = logLevel;
            DependantTask = dependantTask;
            DisplayStatus = dependantTask is null
                ? Loc.Get("queue.status_ready")
                : Loc.Get("queue.status_waiting");

            Status = dependantTask is null ? QueueItemStatus.Ready : QueueItemStatus.Waiting;
            CanCancel = true;
        }

        public QueueTaskKind Kind { get; }
        public string Title { get; }
        public string TaskType => Kind switch
        {
            QueueTaskKind.VodDownload => Loc.Get("queue.kind_vod"),
            QueueTaskKind.ClipDownload => Loc.Get("queue.kind_clip"),
            QueueTaskKind.ChatDownload => Loc.Get("queue.kind_chat"),
            QueueTaskKind.ChatUpdate => Loc.Get("queue.kind_update"),
            QueueTaskKind.ChatRender => Loc.Get("queue.kind_render"),
            _ => Loc.Get("queue.kind_task"),
        };

        public string SourceId { get; }
        public byte[]? ThumbnailBytes { get; }
        public QueueItemViewModel? DependantTask { get; }
        internal object Options => _options;
        public bool HasThumbnail => ThumbnailBytes is { Length: > 0 };

        public string OutputFile => _options switch
        {
            VideoDownloadOptions vod => vod.Filename,
            ClipDownloadOptions clip => clip.Filename,
            ChatDownloadOptions chat => chat.Filename,
            ChatUpdateOptions update => update.OutputFile,
            ChatRenderOptions render => render.OutputFile,
            _ => string.Empty,
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRetry))]
        [NotifyPropertyChangedFor(nameof(CanRemove))]
        [NotifyPropertyChangedFor(nameof(ShowProgress))]
        [NotifyPropertyChangedFor(nameof(StatusImage))]
        [NotifyPropertyChangedFor(nameof(IsAnimatedStatus))]
        [NotifyPropertyChangedFor(nameof(ShowStatusGif))]
        public partial QueueItemStatus Status { get; private set; }

        [ObservableProperty]
        public partial string DisplayStatus { get; private set; }

        [ObservableProperty]
        public partial double Progress { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        public partial Exception? Exception { get; private set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        public partial bool CanCancel { get; private set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
        public partial bool CanReinitialize { get; private set; }

        public bool CanRetry => CanReinitialize;
        public bool CanRemove => Status is not QueueItemStatus.Running and not QueueItemStatus.Stopping;
        public bool HasError => Exception is not null;
        public bool ShowProgress => Status is QueueItemStatus.Running or QueueItemStatus.Stopping;
        public bool IsAnimatedStatus => Status is QueueItemStatus.Running or QueueItemStatus.Ready or QueueItemStatus.Waiting or QueueItemStatus.Stopping;
        public bool ShowStatusGif => _appStatus?.ShowStatusImage == true && IsAnimatedStatus && StatusImage is not null;

        public string? StatusImage => Status switch
        {
            QueueItemStatus.Running => "avares://TwitchDownloaderAvalonia/Assets/Status/ppOverheat.gif",
            QueueItemStatus.Stopping => "avares://TwitchDownloaderAvalonia/Assets/Status/ppStretch.gif",
            QueueItemStatus.Ready or QueueItemStatus.Waiting => "avares://TwitchDownloaderAvalonia/Assets/Status/ppHop.gif",
            _ => null,
        };

        public void AttachHost(QueueService queue, DialogService dialogs, AppStatus status)
        {
            _queue = queue;
            _dialogs = dialogs;
            _appStatus = status;
            status.PropertyChanged += OnAppStatusChanged;
            OnPropertyChanged(nameof(ShowStatusGif));
        }

        public void DetachHost()
        {
            if (_appStatus is not null)
                _appStatus.PropertyChanged -= OnAppStatusChanged;

            _appStatus = null;
            _queue = null;
            _dialogs = null;
        }

        protected override void DisposeCore()
        {
            DetachHost();
        }

        private void OnAppStatusChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(AppStatus.ShowStatusImage) or nameof(AppStatus.ReduceMotion))
                OnPropertyChanged(nameof(ShowStatusGif));
        }

        public bool CanRun()
        {
            switch (QueueDependency.Evaluate(Status, DependantTask?.Status))
            {
                case QueueRunDecision.Ready:
                case QueueRunDecision.Start:
                    return true;
                case QueueRunDecision.CancelBecauseDependantFailed:
                    ChangeStatus(QueueItemStatus.Canceled);
                    CanReinitialize = true;
                    NotifyActionState();
                    return false;
                default:
                    return false;
            }
        }

        public async Task RunAsync()
        {
            if (_tokenSource.IsCancellationRequested)
            {
                ReplaceTokenSource();
                ChangeStatus(QueueItemStatus.Canceled);
                CanReinitialize = true;
                NotifyActionState();
                return;
            }

            var progress = new AvaloniaTaskProgress(
                _logLevel,
                percent => Progress = percent,
                status =>
                {
                    _hasLiveProgressStatus = true;
                    DisplayStatus = status;
                });

            try
            {
                if (TryGetDelayVideoId(out var videoId))
                {
                    var delayed = await DelayUntilVideoOfflineAsync(videoId, progress, _tokenSource.Token);
                    if (!delayed)
                    {
                        ChangeStatus(QueueItemStatus.Failed);
                        CanReinitialize = true;
                        return;
                    }

                    if (_tokenSource.IsCancellationRequested)
                    {
                        ChangeStatus(QueueItemStatus.Canceled);
                        CanReinitialize = true;
                        return;
                    }
                }

                ChangeStatus(QueueItemStatus.Running);
                await Task.Run(async () => await ExecuteCoreAsync(progress, _tokenSource.Token));

                if (_tokenSource.IsCancellationRequested)
                {
                    ChangeStatus(QueueItemStatus.Canceled);
                    CanReinitialize = true;
                }
                else
                {
                    progress.ReportProgress(100);
                    ChangeStatus(QueueItemStatus.Finished);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _tokenSource.IsCancellationRequested)
            {
                ChangeStatus(QueueItemStatus.Canceled);
                CanReinitialize = true;
            }
            catch (Exception ex)
            {
                Exception = ex;
                ChangeStatus(QueueItemStatus.Failed);
                CanReinitialize = true;
            }
            finally
            {
                try
                {
                    _tokenSource.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }

                _tokenSource = new CancellationTokenSource();
                NotifyActionState();
            }
        }

        public void RequestCancel()
        {
            if (!CanCancel)
                return;

            try
            {
                _tokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            ChangeStatus(Status is QueueItemStatus.Running ? QueueItemStatus.Stopping : QueueItemStatus.Canceled);
            if (Status is QueueItemStatus.Canceled)
                CanReinitialize = true;

            NotifyActionState();
        }

        public void Reinitialize()
        {
            if (!CanReinitialize)
                return;

            Progress = 0;
            Exception = null;
            CanReinitialize = false;
            _hasLiveProgressStatus = false;
            ReplaceTokenSource();
            ChangeStatus(DependantTask is null ? QueueItemStatus.Ready : QueueItemStatus.Waiting);
            NotifyActionState();
        }

        private void ReplaceTokenSource()
        {
            var previous = _tokenSource;
            _tokenSource = new CancellationTokenSource();
            try
            {
                previous.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel() => _queue?.Cancel(this);

        [RelayCommand(CanExecute = nameof(CanRetry))]
        private void Retry() => _queue?.Retry(this);

        [RelayCommand(CanExecute = nameof(CanRemove))]
        private void Remove() => _queue?.Remove(this);

        [RelayCommand]
        private void MoveUp() => _queue?.MoveUp(this);

        [RelayCommand]
        private void MoveDown() => _queue?.MoveDown(this);

        [RelayCommand(CanExecute = nameof(HasError))]
        private Task ShowErrorAsync()
        {
            if (Exception is null || _dialogs is null)
                return Task.CompletedTask;

            return _dialogs.ShowErrorAsync(Loc.Get("queue.task_error"), Exception.ToString());
        }

        [RelayCommand]
        private Task CopyPathAsync()
        {
            return _dialogs is not null && !string.IsNullOrWhiteSpace(OutputFile)
                ? _dialogs.CopyTextAsync(OutputFile)
                : Task.CompletedTask;
        }

        [RelayCommand]
        private void OpenFolder()
        {
            var path = OutputFile;
            if (string.IsNullOrWhiteSpace(path))
                return;

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var args = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{directory}\"";
                    Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
                    return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    if (File.Exists(path))
                        Process.Start("open", ["-R", path]);
                    else
                        Process.Start("open", [directory]);
                    return;
                }

                Process.Start(new ProcessStartInfo("xdg-open", directory) { UseShellExecute = true });
            }
            catch
            {
                // Opening the folder is best-effort.
            }
        }

        public static QueueItemViewModel CreateVod(
            VideoDownloadOptions options,
            string title,
            byte[]? thumbnail,
            LogLevel logLevel)
        {
            return new QueueItemViewModel(
                QueueTaskKind.VodDownload,
                options,
                title,
                thumbnail,
                options.Id.ToString(),
                logLevel,
                dependantTask: null);
        }

        public static QueueItemViewModel CreateClip(
            ClipDownloadOptions options,
            string title,
            byte[]? thumbnail,
            LogLevel logLevel)
        {
            return new QueueItemViewModel(
                QueueTaskKind.ClipDownload,
                options,
                title,
                thumbnail,
                options.Id,
                logLevel,
                dependantTask: null);
        }

        public static QueueItemViewModel CreateChat(
            ChatDownloadOptions options,
            string title,
            byte[]? thumbnail,
            LogLevel logLevel,
            QueueItemViewModel? dependantTask = null)
        {
            return new QueueItemViewModel(
                QueueTaskKind.ChatDownload,
                options,
                title,
                thumbnail,
                options.Id,
                logLevel,
                dependantTask);
        }

        public static QueueItemViewModel CreateChatUpdate(
            ChatUpdateOptions options,
            string title,
            byte[]? thumbnail,
            LogLevel logLevel)
        {
            return new QueueItemViewModel(
                QueueTaskKind.ChatUpdate,
                options,
                title,
                thumbnail,
                options.InputFile,
                logLevel,
                dependantTask: null);
        }

        public static QueueItemViewModel CreateChatRender(
            ChatRenderOptions options,
            string title,
            byte[]? thumbnail,
            LogLevel logLevel,
            QueueItemViewModel? dependantTask = null)
        {
            return new QueueItemViewModel(
                QueueTaskKind.ChatRender,
                options,
                title,
                thumbnail,
                options.InputFile,
                logLevel,
                dependantTask);
        }

        private bool TryGetDelayVideoId(out long videoId)
        {
            switch (_options)
            {
                case VideoDownloadOptions { DelayDownload: true } vod:
                    videoId = vod.Id;
                    return true;
                case ChatDownloadOptions { DelayDownload: true } chat when long.TryParse(chat.Id, out videoId):
                    return true;
                default:
                    videoId = 0;
                    return false;
            }
        }

        private async Task<bool> DelayUntilVideoOfflineAsync(
            long videoId,
            AvaloniaTaskProgress progress,
            CancellationToken cancellationToken)
        {
            try
            {
                ChangeStatus(QueueItemStatus.Waiting);
                using var videoMonitor = new LiveVideoMonitor(videoId, progress);
                while (await videoMonitor.IsVideoRecording(cancellationToken))
                {
                    var waitTime = Random.Shared.NextDouble(8, 14);
                    await Task.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && cancellationToken.IsCancellationRequested)
            {
                return true;
            }
            catch (Exception ex)
            {
                Exception = ex;
                return false;
            }

            return true;
        }

        private async Task ExecuteCoreAsync(AvaloniaTaskProgress progress, CancellationToken cancellationToken)
        {
            switch (_options)
            {
                case VideoDownloadOptions vod:
                    await new VideoDownloader(vod, progress).DownloadAsync(cancellationToken);
                    break;
                case ClipDownloadOptions clip:
                    await new ClipDownloader(clip, progress).DownloadAsync(cancellationToken);
                    break;
                case ChatDownloadOptions chat:
                    await new ChatDownloader(chat, progress).DownloadAsync(cancellationToken);
                    break;
                case ChatUpdateOptions update:
                    var updater = new ChatUpdater(update, progress);
                    await updater.ParseJsonAsync(cancellationToken);
                    await updater.UpdateAsync(cancellationToken);
                    break;
                case ChatRenderOptions render:
                    var renderer = new ChatRenderer(render, progress);
                    try
                    {
                        await renderer.ParseJsonAsync(cancellationToken);
                        await renderer.RenderVideoAsync(cancellationToken);
                    }
                    finally
                    {
                        renderer.Dispose();
                    }
                    break;
            }
        }

        private void ChangeStatus(QueueItemStatus status)
        {
            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
            return;

            void Apply()
            {
                Status = status;
                if (status is not QueueItemStatus.Running)
                    _hasLiveProgressStatus = false;

                DisplayStatus = status is QueueItemStatus.Running && _hasLiveProgressStatus
                    ? DisplayStatus
                    : StatusLabel(status);

                CanCancel = status is not QueueItemStatus.Canceled
                    and not QueueItemStatus.Failed
                    and not QueueItemStatus.Finished
                    and not QueueItemStatus.Stopping;
            }
        }

        protected override void OnCultureChanged(object? sender, EventArgs e)
        {
            Notify(nameof(TaskType));
            if (Status is QueueItemStatus.Running && _hasLiveProgressStatus)
                return;

            DisplayStatus = StatusLabel(Status);
        }

        private static string StatusLabel(QueueItemStatus status) => status switch
        {
            QueueItemStatus.Ready => Loc.Get("queue.status_ready"),
            QueueItemStatus.Waiting => Loc.Get("queue.status_waiting"),
            QueueItemStatus.Running => Loc.Get("queue.status_running"),
            QueueItemStatus.Stopping => Loc.Get("queue.status_canceling"),
            QueueItemStatus.Finished => Loc.Get("queue.status_finished"),
            QueueItemStatus.Failed => Loc.Get("queue.status_failed"),
            QueueItemStatus.Canceled => Loc.Get("queue.status_canceled"),
            _ => status.ToString(),
        };

        private void NotifyActionState()
        {
            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
            return;

            void Apply()
            {
                OnPropertyChanged(nameof(CanRetry));
                OnPropertyChanged(nameof(CanRemove));
                OnPropertyChanged(nameof(HasError));
                CancelCommand.NotifyCanExecuteChanged();
                RetryCommand.NotifyCanExecuteChanged();
                RemoveCommand.NotifyCanExecuteChanged();
                ShowErrorCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
