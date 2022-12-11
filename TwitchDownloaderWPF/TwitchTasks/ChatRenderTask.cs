using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloader.TwitchTasks
{
    class ChatRenderTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; set; } = TwitchTaskStatus.Ready;
        public ChatRenderOptions DownloadOptions { get; set; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; set; } = "Chat Render";

        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            Status = TwitchTaskStatus.Stopping;
            OnPropertyChanged("Status");
            TokenSource.Cancel();
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
                if (DependantTask.Status == TwitchTaskStatus.Failed || DependantTask.Status == TwitchTaskStatus.Cancelled)
                {
                    Status = TwitchTaskStatus.Cancelled;
                    return false;
                }
            }
            return false;
        }

        public async Task RunAsync()
        {
            ChatRenderer downloader = new ChatRenderer(DownloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            Status = TwitchTaskStatus.Running;
            OnPropertyChanged("Status");
            try
            {
                await downloader.ParseJsonAsync();
                await downloader.RenderVideoAsync(progress, TokenSource.Token);
                if (TokenSource.IsCancellationRequested)
                {
                    Status = TwitchTaskStatus.Cancelled;
                    OnPropertyChanged("Status");
                }
                else
                {
                    Status = TwitchTaskStatus.Finished;
                    Progress = 100;
                    OnPropertyChanged("Progress");
                    OnPropertyChanged("Status");
                }
            }
            catch
            {
                Status = TwitchTaskStatus.Failed;
                OnPropertyChanged("Status");
            }
        }

        private void Progress_ProgressChanged(object sender, ProgressReport e)
        {
            if (e.ReportType == ReportType.Percent)
            {
                int percent = (int)e.Data;
                if (percent > Progress)
                {
                    Progress = percent;
                    OnPropertyChanged("Progress");
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
