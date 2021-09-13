using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

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
        string Title { get; set; }
        ImageSource Thumbnail { get; set; }
        int Progress { get; set; }
        TwitchTaskStatus Status { get; set; }
        CancellationTokenSource TokenSource { get; set; }
        ITwitchTask DependantTask { get; set; }
        string TaskType { get; set; }

        Task RunAsync();
        void Cancel();
        bool CanRun();
    }
}
