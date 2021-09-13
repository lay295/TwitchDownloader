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
    class ClipDownloadTask : ITwitchTask
    {
        public string Title { get; set; }
        public ImageSource Thumbnail { get; set; }
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; set; } = TwitchTaskStatus.Ready;
        public ClipDownloadOptions DownloadOptions { get; set; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; set; } = "Clip Download";

        public event PropertyChangedEventHandler PropertyChanged;

        public void Cancel()
        {
            
        }

        public bool CanRun()
        {
            return Status == TwitchTaskStatus.Ready;
        }

        public async Task RunAsync()
        {
            ClipDownloader downloader = new ClipDownloader(DownloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            Status = TwitchTaskStatus.Running;
            OnPropertyChanged("Status");
            try
            {
                await downloader.DownloadAsync();
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
            if (e.reportType == ReportType.Percent)
            {
                int percent = (int)e.data;
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
