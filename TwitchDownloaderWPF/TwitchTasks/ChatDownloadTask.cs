﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderWPF.TwitchTasks
{
    class ChatDownloadTask : ITwitchTask
    {
        public TaskData Info { get; set; } = new TaskData();
        public int Progress { get; set; }
        public TwitchTaskStatus Status { get; private set; } = TwitchTaskStatus.Ready;
        public ChatDownloadOptions DownloadOptions { get; set; }
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public ITwitchTask DependantTask { get; set; }
        public string TaskType { get; } = Translations.Strings.ChatDownload;
        public TwitchTaskException Exception { get; private set; } = new();
        public string OutputFile => DownloadOptions.Filename;
        public bool CanCancel { get; private set; } = true;

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
            return Status == TwitchTaskStatus.Ready;
        }

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;
            OnPropertyChanged(nameof(Status));

            if (CanCancel && newStatus is TwitchTaskStatus.Canceled or TwitchTaskStatus.Failed or TwitchTaskStatus.Finished or TwitchTaskStatus.Stopping)
            {
                CanCancel = false;
                OnPropertyChanged(nameof(CanCancel));
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

            ChatDownloader downloader = new ChatDownloader(DownloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            ChangeStatus(TwitchTaskStatus.Running);
            try
            {
                await downloader.DownloadAsync(progress, TokenSource.Token);
                if (TokenSource.IsCancellationRequested)
                {
                    ChangeStatus(TwitchTaskStatus.Canceled);
                }
                else
                {
                    Progress = 100;
                    OnPropertyChanged(nameof(Progress));
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
                OnPropertyChanged(nameof(Exception));
            }
            downloader = null;
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
