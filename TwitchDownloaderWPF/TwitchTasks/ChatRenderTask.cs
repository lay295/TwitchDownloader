using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks
{
    class ChatRenderTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();

        private int _progress;
        public int Progress
        {
            get => _progress;
            set
            {
                if (value == _progress) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        private TwitchTaskStatus _status = TwitchTaskStatus.Ready;
        public TwitchTaskStatus Status
        {
            get => _status;
            private set
            {
                if (value == _status) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        public ChatRenderOptions DownloadOptions { get; init; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; } = Translations.Strings.ChatRender;

        private TwitchTaskException _exception = new();
        public TwitchTaskException Exception
        {
            get => _exception;
            private set
            {
                if (Equals(value, _exception)) return;
                _exception = value;
                OnPropertyChanged();
            }
        }

        public string OutputFile => DownloadOptions.OutputFile;

        private bool _canCancel = true;
        public bool CanCancel
        {
            get => _canCancel;
            private set
            {
                if (value == _canCancel) return;
                _canCancel = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            if (!CanCancel)
            {
                return;
            }

            TokenSource.Cancel();

            if (Status == TwitchTaskStatus.Running)
            {
                ChangeStatus(TwitchTaskStatus.Stopping);
                return;
            }

            ChangeStatus(TwitchTaskStatus.Canceled);
        }

        public bool CanRun()
        {
            if (DependantTask == null)
            {
                if (Status == TwitchTaskStatus.Ready)
                {
                    return true;
                }
            }
            else if (Status == TwitchTaskStatus.Waiting)
            {
                if (DependantTask.Status == TwitchTaskStatus.Finished)
                {
                    return true;
                }
                if (DependantTask.Status is TwitchTaskStatus.Failed or TwitchTaskStatus.Canceled)
                {
                    ChangeStatus(TwitchTaskStatus.Canceled);
                    return false;
                }
            }
            return false;
        }

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;

            if (CanCancel && newStatus is TwitchTaskStatus.Canceled or TwitchTaskStatus.Failed or TwitchTaskStatus.Finished or TwitchTaskStatus.Stopping)
            {
                CanCancel = false;
            }
        }

        public async Task RunAsync()
        {
            if (TokenSource.IsCancellationRequested)
            {
                TokenSource.Dispose();
                ChangeStatus(TwitchTaskStatus.Canceled);
                return;
            }

            var progress = new WpfTaskProgress(i => Progress = i);
            ChatRenderer renderer = new ChatRenderer(DownloadOptions, progress);
            ChangeStatus(TwitchTaskStatus.Running);
            try
            {
                await renderer.ParseJsonAsync(TokenSource.Token);
                await renderer.RenderVideoAsync(TokenSource.Token);
                if (TokenSource.IsCancellationRequested)
                {
                    ChangeStatus(TwitchTaskStatus.Canceled);
                }
                else
                {
                    progress.ReportProgress(100);
                    ChangeStatus(TwitchTaskStatus.Finished);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && TokenSource.IsCancellationRequested)
            {
                ChangeStatus(TwitchTaskStatus.Canceled);
            }
            catch (Exception ex)
            {
                ChangeStatus(TwitchTaskStatus.Failed);
                Exception = new TwitchTaskException(ex);
            }
            renderer.Dispose();
            TokenSource.Dispose();
            GC.Collect(2, GCCollectionMode.Default, false);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
