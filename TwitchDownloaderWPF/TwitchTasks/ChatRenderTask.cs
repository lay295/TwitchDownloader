using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks
{
    internal class ChatRenderTask : TwitchTask
    {
        public ChatRenderOptions DownloadOptions { get; init; }
        public override string TaskType { get; } = Translations.Strings.ChatRender;
        public override string OutputFile => DownloadOptions.OutputFile;

        public override void Reinitialize()
        {
            Progress = 0;
            TokenSource = new CancellationTokenSource();
            Exception = null;
            CanReinitialize = false;
            ChangeStatus(DependantTask is null ? TwitchTaskStatus.Ready : TwitchTaskStatus.Waiting);
        }

        public override bool CanRun()
        {
            if (DependantTask == null)
            {
                return Status == TwitchTaskStatus.Ready;
            }

            if (Status == TwitchTaskStatus.Waiting)
            {
                if (DependantTask.Status == TwitchTaskStatus.Finished)
                {
                    return true;
                }

                if (DependantTask.Status is TwitchTaskStatus.Failed or TwitchTaskStatus.Canceled)
                {
                    ChangeStatus(TwitchTaskStatus.Canceled);
                    CanReinitialize = true;
                    return false;
                }
            }

            return false;
        }

        public override async Task RunAsync()
        {
            if (TokenSource.IsCancellationRequested)
            {
                TokenSource.Dispose();
                ChangeStatus(TwitchTaskStatus.Canceled);
                CanReinitialize = true;
                return;
            }

            var progress = new WpfTaskProgress(i => Progress = i, s => DisplayStatus = s);
            ChatRenderer renderer = new ChatRenderer(DownloadOptions, progress);
            ChangeStatus(TwitchTaskStatus.Running);
            try
            {
                await renderer.ParseJsonAsync(TokenSource.Token);
                await renderer.RenderVideoAsync(TokenSource.Token);
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
            renderer.Dispose();
            TokenSource.Dispose();
            GC.Collect(-1, GCCollectionMode.Default, false);
        }
    }
}