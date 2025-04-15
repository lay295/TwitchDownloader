using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks
{
    internal class ChatDownloadTask : TwitchTask
    {
        public ChatDownloadOptions DownloadOptions { get; init; }
        public override string TaskType { get; } = Translations.Strings.ChatDownload;
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
                DownloadType downloadType = DownloadOptions.Id.All(char.IsDigit) ? DownloadType.Video : DownloadType.Clip;

                if (downloadType == DownloadType.Video)
                {
                    ChangeStatus(TwitchTaskStatus.Waiting);

                    var videoMonitor = new LiveVideoMonitor((long)Convert.ToDouble(DownloadOptions.Id));
                    while (await videoMonitor.IsVideoRecording())
                    {
                        var waitTime = Random.Shared.NextDouble(8, 14);
                        await Task.Delay(TimeSpan.FromSeconds(waitTime));
                    }
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
    }
}