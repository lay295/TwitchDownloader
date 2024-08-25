using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderWPF.TwitchTasks
{
    public enum TwitchTaskStatus
    {
        Waiting,
        Ready,
        Running,
        Failed,
        Finished,
        Stopping,
        Canceled
    }

    public interface ITwitchTask : INotifyPropertyChanged
    {
        TaskData Info { get; set; }
        int Progress { get; }
        TwitchTaskStatus Status { get; }
        string DisplayStatus { get; }
        string StatusImage { get; }
        CancellationTokenSource TokenSource { get; set; }
        ITwitchTask DependantTask { get; set; }
        string TaskType { get; }
        Exception Exception { get; }
        string OutputFile { get; }
        bool CanCancel { get; }

        Task RunAsync();
        void Cancel();
        bool CanRun();
    }
}
