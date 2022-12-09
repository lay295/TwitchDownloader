﻿using System;
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
    class ChatDownloadTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; set; } = TwitchTaskStatus.Ready;
        public ChatDownloadOptions DownloadOptions { get; set; }
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
            ChatDownloader downloader = new ChatDownloader(DownloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            Status = TwitchTaskStatus.Running;
            OnPropertyChanged("Status");
            try
            {
                await downloader.DownloadAsync(progress, TokenSource.Token);
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
