using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks
{
    internal class VodDownloadTask : TwitchTask
    {
        public VideoDownloadOptions DownloadOptions { get; init; }
        public override string TaskType { get; } = Translations.Strings.VodDownload;
        public override string OutputFile => DownloadOptions.Filename;

        public override void Reinitialize()
        {
            Progress = 0;
            TokenSource = new CancellationTokenSource();
            Exception = null;
            CanReinitialize = false;
            ChangeStatus(TwitchTaskStatus.Ready);
        }

        public override bool CanRun()
        {
            return Status == TwitchTaskStatus.Ready;
        }

        public override async Task RunAsync()
        {
            if (DownloadOptions.DelayDownload)
            {
                ChangeStatus(TwitchTaskStatus.Waiting);

                var videoMonitor = new LiveVideoMonitor(DownloadOptions.Id);
                while (await videoMonitor.IsVideoRecording())
                {
                    var waitTime = Random.Shared.NextDouble(8, 14);
                    await Task.Delay(TimeSpan.FromSeconds(waitTime));
                }
            }

            if (TokenSource.IsCancellationRequested)
            {
                TokenSource.Dispose();
                ChangeStatus(TwitchTaskStatus.Canceled);
                CanReinitialize = true;
                return;
            }

            var progress = new WpfTaskProgress(i => Progress = i, s => DisplayStatus = s);
            VideoDownloader downloader = new VideoDownloader(DownloadOptions, progress);
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
    }
}