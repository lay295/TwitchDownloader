using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed partial class QueueService : ObservableObject
    {
        private readonly SettingsService _settings;
        private readonly AppStatus _status;
        private readonly DialogService _dialogs;
        private bool _pumpQueued;

        public QueueService(SettingsService settings, AppStatus status, DialogService dialogs)
        {
            _settings = settings;
            _status = status;
            _dialogs = dialogs;

            Items.CollectionChanged += OnItemsChanged;
            LocalizationService.Current.CultureChanged += (_, _) => RefreshAppStatus();
        }

        public ObservableCollection<QueueItemViewModel> Items { get; } = [];

        public int LimitVod
        {
            get => Math.Clamp(_settings.Current.LimitVod, 1, 50);
            set
            {
                var clamped = Math.Clamp(value, 1, 50);
                if (_settings.Current.LimitVod == clamped)
                    return;

                _settings.Current.LimitVod = clamped;
                _settings.Save();
                OnPropertyChanged();
                RequestPump();
            }
        }

        public int LimitClip
        {
            get => Math.Clamp(_settings.Current.LimitClip, 1, 50);
            set
            {
                var clamped = Math.Clamp(value, 1, 50);
                if (_settings.Current.LimitClip == clamped)
                    return;

                _settings.Current.LimitClip = clamped;
                _settings.Save();
                OnPropertyChanged();
                RequestPump();
            }
        }

        public int LimitChat
        {
            get => Math.Clamp(_settings.Current.LimitChat, 1, 50);
            set
            {
                var clamped = Math.Clamp(value, 1, 50);
                if (_settings.Current.LimitChat == clamped)
                    return;

                _settings.Current.LimitChat = clamped;
                _settings.Save();
                OnPropertyChanged();
                RequestPump();
            }
        }

        public int LimitRender
        {
            get => Math.Clamp(_settings.Current.LimitRender, 1, 50);
            set
            {
                var clamped = Math.Clamp(value, 1, 50);
                if (_settings.Current.LimitRender == clamped)
                    return;

                _settings.Current.LimitRender = clamped;
                _settings.Save();
                OnPropertyChanged();
                RequestPump();
            }
        }

        [ObservableProperty]
        public partial bool HasItems { get; private set; }

        [ObservableProperty]
        public partial bool HasFinishedItems { get; private set; }

        [ObservableProperty]
        public partial bool CanCancelAll { get; private set; }

        public LogLevel LogLevel => (LogLevel)_settings.Current.LogLevels;

        public void NotifyLimitsChanged()
        {
            OnPropertyChanged(nameof(LimitVod));
            OnPropertyChanged(nameof(LimitClip));
            OnPropertyChanged(nameof(LimitChat));
            OnPropertyChanged(nameof(LimitRender));
            RequestPump();
        }

        public QueueItemViewModel EnqueueVod(VideoDownloadOptions options, string title, byte[]? thumbnail)
        {
            var item = QueueItemViewModel.CreateVod(options, title, thumbnail, LogLevel);
            Enqueue(item);
            return item;
        }

        public QueueItemViewModel EnqueueClip(ClipDownloadOptions options, string title, byte[]? thumbnail)
        {
            var item = QueueItemViewModel.CreateClip(options, title, thumbnail, LogLevel);
            Enqueue(item);
            return item;
        }

        public QueueItemViewModel EnqueueChat(ChatDownloadOptions options, string title, byte[]? thumbnail, QueueItemViewModel? dependant = null)
        {
            var item = QueueItemViewModel.CreateChat(options, title, thumbnail, LogLevel, dependant);
            Enqueue(item);
            return item;
        }

        public QueueItemViewModel EnqueueChatUpdate(ChatUpdateOptions options, string title, byte[]? thumbnail)
        {
            var item = QueueItemViewModel.CreateChatUpdate(options, title, thumbnail, LogLevel);
            Enqueue(item);
            return item;
        }

        public QueueItemViewModel EnqueueChatRender(ChatRenderOptions options, string title, byte[]? thumbnail, QueueItemViewModel? dependant = null)
        {
            var item = QueueItemViewModel.CreateChatRender(options, title, thumbnail, LogLevel, dependant);
            Enqueue(item);
            return item;
        }

        public void Enqueue(QueueItemViewModel item)
        {
            item.AttachHost(this, _dialogs, _status);
            item.PropertyChanged += OnItemPropertyChanged;
            Items.Add(item);
            RequestPump();
            RefreshDerived();
            RefreshAppStatus();
        }

        public void EnqueueRange(IEnumerable<QueueItemViewModel> items)
        {
            foreach (var item in items)
            {
                item.AttachHost(this, _dialogs, _status);
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }

            RequestPump();
            RefreshDerived();
            RefreshAppStatus();
        }

        public void Cancel(QueueItemViewModel item) => item.RequestCancel();

        public void Retry(QueueItemViewModel item)
        {
            item.Reinitialize();
            RequestPump();
            RefreshAppStatus();
        }

        public void Remove(QueueItemViewModel item)
        {
            if (!item.CanRemove)
                return;

            if (item.CanCancel)
                item.RequestCancel();

            Detach(item);
            Items.Remove(item);
            RequestPump();
            RefreshDerived();
            RefreshAppStatus();
        }

        public void MoveUp(QueueItemViewModel item)
        {
            var index = Items.IndexOf(item);
            if (index <= 0)
                return;

            Items.Move(index, index - 1);
        }

        public void MoveDown(QueueItemViewModel item)
        {
            var index = Items.IndexOf(item);
            if (index < 0 || index >= Items.Count - 1)
                return;

            Items.Move(index, index + 1);
        }

        public void CancelAll()
        {
            foreach (var item in Items.ToArray())
            {
                if (item.CanCancel)
                    item.RequestCancel();
            }

            RequestPump();
            RefreshAppStatus();
        }

        public void ClearFinished()
        {
            var finished = Items
                .Where(item => item.Status is QueueItemStatus.Finished or QueueItemStatus.Failed or QueueItemStatus.Canceled)
                .ToArray();

            foreach (var item in finished)
            {
                Detach(item);
                Items.Remove(item);
            }

            RefreshDerived();
            RefreshAppStatus();
        }

        private void Detach(QueueItemViewModel item)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshDerived();

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(QueueItemViewModel.Status) or nameof(QueueItemViewModel.CanCancel) or nameof(QueueItemViewModel.CanRemove))
            {
                RefreshDerived();
                RequestPump();
            }

            if (e.PropertyName is nameof(QueueItemViewModel.Status) or nameof(QueueItemViewModel.DisplayStatus) or nameof(QueueItemViewModel.Progress))
                RefreshAppStatus();
        }

        private void RequestPump()
        {
            if (_pumpQueued)
                return;

            _pumpQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _pumpQueued = false;
                Pump();
            });
        }

        private void Pump()
        {
            var runningVod = 0;
            var runningClip = 0;
            var runningChat = 0;
            var runningRender = 0;

            foreach (var item in Items)
            {
                if (item.Status is not QueueItemStatus.Running)
                    continue;

                switch (item.Kind)
                {
                    case QueueTaskKind.VodDownload:
                        runningVod++;
                        break;
                    case QueueTaskKind.ClipDownload:
                        runningClip++;
                        break;
                    case QueueTaskKind.ChatDownload:
                    case QueueTaskKind.ChatUpdate:
                        runningChat++;
                        break;
                    case QueueTaskKind.ChatRender:
                        runningRender++;
                        break;
                }
            }

            foreach (var item in Items)
            {
                if (!item.CanRun())
                    continue;

                var started = item.Kind switch
                {
                    QueueTaskKind.VodDownload when runningVod < LimitVod => runningVod++ >= 0,
                    QueueTaskKind.ClipDownload when runningClip < LimitClip => runningClip++ >= 0,
                    QueueTaskKind.ChatDownload or QueueTaskKind.ChatUpdate when runningChat < LimitChat => runningChat++ >= 0,
                    QueueTaskKind.ChatRender when runningRender < LimitRender => runningRender++ >= 0,
                    _ => false,
                };

                if (started)
                    _ = item.RunAsync();
            }
        }

        private void RefreshDerived()
        {
            HasItems = Items.Count > 0;
            HasFinishedItems = Items.Any(item =>
                item.Status is QueueItemStatus.Finished or QueueItemStatus.Failed or QueueItemStatus.Canceled);
            CanCancelAll = Items.Any(item => item.CanCancel);
        }

        private void RefreshAppStatus()
        {
            var unfinished = Items.Count(item => item.Status is QueueItemStatus.Waiting or QueueItemStatus.Ready or QueueItemStatus.Running or QueueItemStatus.Stopping);
            _status.QueueCount = unfinished;

            var stopping = Items.FirstOrDefault(item => item.Status == QueueItemStatus.Stopping);
            if (stopping is not null)
            {
                _status.Set(AppStatusKind.Canceling, Loc.Get("status.task_running", stopping.Title, stopping.DisplayStatus), stopping.Progress, stopping.ThumbnailBytes);
                return;
            }

            var running = Items.FirstOrDefault(item => item.Status == QueueItemStatus.Running);
            if (running is not null)
            {
                var extra = Items.Count(item => item.Status == QueueItemStatus.Running);
                var message = extra > 1
                    ? Loc.Get("status.task_running_extra", running.Title, running.DisplayStatus, extra - 1)
                    : Loc.Get("status.task_running", running.Title, running.DisplayStatus);

                _status.Set(AppStatusKind.Running, message, running.Progress, running.ThumbnailBytes);
                return;
            }

            var waiting = Items.FirstOrDefault(item => item.Status is QueueItemStatus.Waiting or QueueItemStatus.Ready);
            if (unfinished > 0)
            {
                _status.Set(AppStatusKind.Idle, Loc.Get("status.waiting_in_queue", unfinished), 0, waiting?.ThumbnailBytes);
                return;
            }

            var failed = Items.LastOrDefault(item => item.Status == QueueItemStatus.Failed);
            if (failed is not null)
            {
                _status.Set(AppStatusKind.Error, Loc.Get("status.task_failed", failed.Title), 0, failed.ThumbnailBytes);
                return;
            }

            _status.Set(AppStatusKind.Idle, Loc.Get("status.idle"), 0);
        }
    }
}
