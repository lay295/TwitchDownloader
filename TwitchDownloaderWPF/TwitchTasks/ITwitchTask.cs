using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloader.TwitchTasks
{
    public enum TwitchTaskStatus
    {
        Waiting,
        Ready,
        Running,
        Failed,
        Finished,
        Stopping,
        Cancelled
    }

    public interface ITwitchTask : INotifyPropertyChanged
    {
        TaskData Info { get; set; }
        int Progress { get; set; }
        TwitchTaskStatus Status { get; }
        CancellationTokenSource TokenSource { get; set; }
        ITwitchTask DependantTask { get; set; }
        string TaskType { get; }
        TwitchTaskException Exception { get; }

        Task RunAsync();
        void Cancel();
        bool CanRun();
        void ChangeStatus(TwitchTaskStatus newStatus);
    }
}
