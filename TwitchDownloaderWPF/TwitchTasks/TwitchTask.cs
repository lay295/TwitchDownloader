using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderWPF.Properties;

namespace TwitchDownloaderWPF.TwitchTasks
{
    public abstract class TwitchTask : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public TaskData Info { get; } = new();

        private int _progress;
        public int Progress
        {
            get => _progress;
            protected set => SetField(ref _progress, value);
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
            protected set => SetField(ref _displayStatus, value);
        }

        private string _statusImage;
        public string StatusImage
        {
            get => _statusImage;
            private set => SetField(ref _statusImage, value);
        }

        protected CancellationTokenSource TokenSource { get; set; } = new();
        public TwitchTask DependantTask { get; init; }
        public abstract string TaskType { get; }

        private Exception _exception;
        public Exception Exception
        {
            get => _exception;
            protected set => SetField(ref _exception, value);
        }

        public abstract string OutputFile { get; }

        private bool _canCancel;
        public bool CanCancel
        {
            get => _canCancel;
            protected set => SetField(ref _canCancel, value);
        }

        private bool _canReinitialize;
        public bool CanReinitialize
        {
            get => _canReinitialize;
            protected set => SetField(ref _canReinitialize, value);
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