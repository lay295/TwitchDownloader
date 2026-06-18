using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks
{
    public abstract class TwitchTask : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public TaskData Info { get; } = new();

        public int Progress
        {
            get;
            protected set => SetField(ref field, value);
        }

        public TwitchTaskStatus Status
        {
            get;
            private set => SetField(ref field, value);
        } = TwitchTaskStatus.Ready;

        public string DisplayStatus
        {
            get;
            protected set => SetField(ref field, value);
        }

        public string StatusImage
        {
            get;
            private set => SetField(ref field, value);
        }

        protected CancellationTokenSource TokenSource { get; set; } = new();
        public TwitchTask DependantTask { get; init; }
        public abstract string TaskType { get; }

        public Exception Exception
        {
            get;
            protected set => SetField(ref field, value);
        }

        public abstract string OutputFile { get; }

        public bool CanCancel
        {
            get;
            protected set => SetField(ref field, value);
        }

        public bool CanReinitialize
        {
            get;
            protected set => SetField(ref field, value);
        }

        public void Cancel()
        {
            if (!CanCancel)
                return;

            TokenSource.Cancel();

            ChangeStatus(Status is TwitchTaskStatus.Running ? TwitchTaskStatus.Stopping : TwitchTaskStatus.Canceled);
        }

        public abstract void Reinitialize();

        public abstract bool CanRun();

        public abstract Task RunAsync();

        public void ChangeStatus(TwitchTaskStatus newStatus)
        {
            Status = newStatus;
            DisplayStatus = newStatus.ToString();

            CanCancel = newStatus is not TwitchTaskStatus.Canceled and not TwitchTaskStatus.Failed and not TwitchTaskStatus.Finished and not TwitchTaskStatus.Stopping;

            if (Settings.Default.ReduceMotion)
            {
                StatusImage = null;
            }
            else
            {
                StatusImage = newStatus switch
                {
                    TwitchTaskStatus.Running => "Images/ppOverheat.gif",
                    TwitchTaskStatus.Ready or TwitchTaskStatus.Waiting => "Images/ppHop.gif",
                    TwitchTaskStatus.Stopping => "Images/ppStretch.gif",
                    _ => null
                };
            }
        }

        protected async Task<bool> DelayUntilVideoOffline(long videoId, ITaskLogger logger)
        {
            try
            {
                ChangeStatus(TwitchTaskStatus.Waiting);

                var videoMonitor = new LiveVideoMonitor(videoId, logger);
                while (await videoMonitor.IsVideoRecording(TokenSource.Token))
                {
                    var waitTime = Random.Shared.NextDouble(8, 14);
                    await Task.Delay(TimeSpan.FromSeconds(waitTime), TokenSource.Token);
                }

                var thumbUrl = videoMonitor.LatestVideoResponse.data.video.thumbnailURLs.FirstOrDefault();
                await MainWindow.pageQueue.Dispatcher.InvokeAsync(() =>
                {
                    if (ThumbnailService.TryGetThumb(thumbUrl, out var newThumb))
                    {
                        Info.Thumbnail = newThumb;
                        OnPropertyChanged(nameof(Info));
                    }
                }, DispatcherPriority.Normal, TokenSource.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && TokenSource.IsCancellationRequested)
            {
                return true;
            }
            catch (Exception ex)
            {
                Exception = ex;
                return false;
            }

            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}