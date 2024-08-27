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
        TaskData Info { get; }
        int Progress { get; }
        TwitchTaskStatus Status { get; }
        string DisplayStatus { get; }
        string StatusImage { get; }
        CancellationTokenSource TokenSource { get; }
        ITwitchTask DependantTask { get; init; }
        string TaskType { get; }
        Exception Exception { get; }
        string OutputFile { get; }
        bool CanCancel { get; }
        bool CanReinitialize { get; }

        Task RunAsync();
        void Cancel();
        bool CanRun();
        void Reinitialize();
    }
}
