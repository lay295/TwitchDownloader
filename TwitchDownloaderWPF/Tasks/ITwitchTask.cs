using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloader.Tasks;

namespace TwitchDownloader
{
    interface ITwitchTask
    {
        CancellationTokenSource CancellationTokenSource { get; set; }
        string getTitle();
        string getInformation();
        Task runTask(IProgress<ProgressReport> progress);
        void cancelTask();
    }
}
