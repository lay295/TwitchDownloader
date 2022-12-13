using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloader.TwitchTasks
{
    class VodDownloadTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; private set; } = TwitchTaskStatus.Ready;
        public VideoDownloadOptions DownloadOptions { get; set; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; set; } = "VOD Download";
        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            ChangeStatus(TwitchTaskStatus.Stopping);
            TokenSource.Cancel();
        }

        public bool CanRun()
        {
            return Status == TwitchTaskStatus.Ready;
        }

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;
            OnPropertyChanged(nameof(Status));
        }

        public async Task RunAsync()
        {
            VideoDownloader downloader = new VideoDownloader(DownloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            ChangeStatus(TwitchTaskStatus.Running);
            try
            {
                await downloader.DownloadAsync(progress, TokenSource.Token);
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
            catch
            {
                if (TokenSource.IsCancellationRequested)
                {
                    ChangeStatus(TwitchTaskStatus.Cancelled);
                }
                else
                {
                    ChangeStatus(TwitchTaskStatus.Failed);
                }
            }
            downloader = null;
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
