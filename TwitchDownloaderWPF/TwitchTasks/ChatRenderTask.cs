using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderWPF.TwitchTasks
{
    class ChatRenderTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; private set; } = TwitchTaskStatus.Ready;
        public ChatRenderOptions DownloadOptions { get; set; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; } = Translations.Strings.ChatRender;
        public TwitchTaskException Exception { get; private set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            try
            {
                TokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }

            if (Status == TwitchTaskStatus.Running)
            {
                ChangeStatus(TwitchTaskStatus.Stopping);
                return;
            }

            ChangeStatus(TwitchTaskStatus.Cancelled);
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
                if (DependantTask.Status is TwitchTaskStatus.Failed or TwitchTaskStatus.Cancelled)
                {
                    ChangeStatus(TwitchTaskStatus.Cancelled);
                    return false;
                }
            }
            return false;
        }

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;
            OnPropertyChanged(nameof(Status));
        }

        public async Task RunAsync()
        {
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            ChatRenderer renderer = new ChatRenderer(DownloadOptions, progress);
            ChangeStatus(TwitchTaskStatus.Running);
            try
            {
                await renderer.ParseJsonAsync(TokenSource.Token);
                await renderer.RenderVideoAsync(TokenSource.Token);
                if (TokenSource.IsCancellationRequested)
                {
                    ChangeStatus(TwitchTaskStatus.Cancelled);
                }
                else
                {
                    ChangeStatus(TwitchTaskStatus.Finished);
                    Progress = 100;
                    OnPropertyChanged(nameof(Progress));
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException)
            {
                ChangeStatus(TwitchTaskStatus.Cancelled);
            }
            catch (Exception ex)
            {
                ChangeStatus(TwitchTaskStatus.Failed);
                Exception = new TwitchTaskException(ex);
                OnPropertyChanged(nameof(Exception));
            }
            renderer = null;
            TokenSource.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void Progress_ProgressChanged(object sender, ProgressReport e)
        {
            if (e.ReportType == ReportType.Percent)
            {
                int percent = (int)e.Data;
                if (percent > Progress)
                {
                    Progress = percent;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
