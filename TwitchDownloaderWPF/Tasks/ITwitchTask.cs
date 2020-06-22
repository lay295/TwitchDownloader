using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using TwitchDownloader.Tasks;

namespace TwitchDownloader
{
    public interface ITwitchTask
    {
        string Title { get; set; }
        string Information { get; set; }
        ImageSource Preview { get; set; }
        CancellationTokenSource CancellationTokenSource { get; set; }
        Task runTask(IProgress<ProgressReport> progress);
        void cancelTask();
    }
}
