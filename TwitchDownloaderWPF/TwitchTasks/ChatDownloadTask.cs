using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks
{
    internal class ChatDownloadTask : ITwitchTask
    {
        public TaskData Info { get; } = new();

        private int _progress;
        public int Progress
        {
            get => _progress;
            private set => SetField(ref _progress, value);
        }

        private TwitchTaskStatus _status = TwitchTaskStatus.Ready;
        public TwitchTaskStatus Status
        {
            get => _status;
            private set => SetField(ref _status, value);
        }

        private string _displayStatus;
        public string DisplayStatus
        {
            get => _displayStatus;
            private set => SetField(ref _displayStatus, value);
        }

        private string _statusImage;
        public string StatusImage
        {
            get => _statusImage;
            private set => SetField(ref _statusImage, value);
        }

        public ChatDownloadOptions DownloadOptions { get; init; }
        public CancellationTokenSource TokenSource { get; private set; } = new();
        public ITwitchTask DependantTask { get; init; }
        public string TaskType { get; } = Translations.Strings.ChatDownload;

        private Exception _exception;
        public Exception Exception
        {
            get => _exception;
            private set => SetField(ref _exception, value);
        }

        public string OutputFile => DownloadOptions.Filename;

        private bool _canCancel;
        public bool CanCancel
        {
            get => _canCancel;
            private set => SetField(ref _canCancel, value);
        }

        private bool _canReinitialize;
        public bool CanReinitialize
        {
            get => _canReinitialize;
            private set => SetField(ref _canReinitialize, value);
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

        public void Reinitialize()
        {
            Progress = 0;
            TokenSource = new CancellationTokenSource();
            Exception = null;
            CanReinitialize = false;
            ChangeStatus(TwitchTaskStatus.Ready);
        }

        public bool CanRun()
        {
            return Status == TwitchTaskStatus.Ready;
        }

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;
            DisplayStatus = newStatus.ToString();

            CanCancel = newStatus is not TwitchTaskStatus.Canceled and not TwitchTaskStatus.Failed and not TwitchTaskStatus.Finished and not TwitchTaskStatus.Stopping;

            StatusImage = newStatus switch
            {
                TwitchTaskStatus.Running => "Images/ppOverheat.gif",
                TwitchTaskStatus.Ready or TwitchTaskStatus.Waiting => "Images/ppHop.gif",
                TwitchTaskStatus.Stopping => "Images/ppStretch.gif",
                _ => null
            };
        }

        public async Task RunAsync()
        {
            if (TokenSource.IsCancellationRequested)
            {
                TokenSource.Dispose();
                ChangeStatus(TwitchTaskStatus.Canceled);
                CanReinitialize = true;
                return;
            }

            var progress = new WpfTaskProgress(i => Progress = i, s => DisplayStatus = s);
            ChatDownloader downloader = new ChatDownloader(DownloadOptions, progress);
            ChangeStatus(TwitchTaskStatus.Running);
            try
            {
                await downloader.DownloadAsync(TokenSource.Token);
                if (TokenSource.IsCancellationRequested)
                {
                    ChangeStatus(TwitchTaskStatus.Canceled);
                    CanReinitialize = true;
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
                CanReinitialize = true;
            }
            catch (Exception ex)
            {
                ChangeStatus(TwitchTaskStatus.Failed);
                Exception = ex;
                CanReinitialize = true;
            }
            TokenSource.Dispose();
            GC.Collect(-1, GCCollectionMode.Default, false);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
