using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloader.TwitchTasks
{
    class ChatUpdateTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; set; } = TwitchTaskStatus.Ready;
        public ChatUpdateOptions UpdateOptions { get; set; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; set; } = "Chat Download";

        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            Status = TwitchTaskStatus.Stopping;
            OnPropertyChanged("Status");
            TokenSource.Cancel();
        }

        public bool CanRun()
        {
            return Status == TwitchTaskStatus.Ready;
        }

        public async Task RunAsync()
        {
            ChatUpdater updater = new ChatUpdater(UpdateOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            Status = TwitchTaskStatus.Running;
            OnPropertyChanged("Status");
            try
            {
                await updater.ParseJsonAsync();
                await updater.UpdateAsync(progress, TokenSource.Token);
                if (TokenSource.IsCancellationRequested)
                {
                    Status = TwitchTaskStatus.Cancelled;
                    OnPropertyChanged("Status");
                }
                else
                {
                    Progress = 100;
                    OnPropertyChanged("Progress");
                    Status = TwitchTaskStatus.Finished;
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
