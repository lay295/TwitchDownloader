using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.Tests.TestHelpers;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF.Tests.TaskTests
{
    // Tests VodDownloadTask's "delay download until the stream ends" option (the shared lifecycle is covered by TwitchTaskTests). A delayed
    // task sits in Waiting while LiveVideoMonitor reports the VOD as recording; the user must be able to cancel it during that wait.
    // GQL is faked to always answer RECORDING, so the task never reaches the real download.
    [Collection(StaticHttpClientCollection.NAME)]
    public sealed class VodDownloadTaskTests : IDisposable
    {
        private readonly TempDirectory _tempDir = new();

        public void Dispose() => _tempDir.Dispose();

        // Verifies a delayed VOD download waits (status Waiting, cancellable) while the stream is live, and that cancelling during the wait
        // ends the task Canceled and retryable, with no error and no output file.
        // Why: the wait loop sleeps 8-14 s between polls; cancellation has to interrupt that sleep and be reported as a cancel, not as a
        // failure (which would show an error dialog for something the user asked for).
        [Fact]
        public async Task DelayedDownload_WaitsWhileLive_AndCanBeCanceledDuringTheWait()
        {
            var handler = FakeGql.VideoStatus("RECORDING");
            using var _ = FakeGql.Use(handler);
            var task = new VodDownloadTask
            {
                DownloadOptions = new VideoDownloadOptions { Id = 920_000_001, DelayDownload = true, Filename = _tempDir.Combine("vod.mp4"), TempFolder = _tempDir.Path }
            };

            var run = task.RunAsync();
            await WaitUntil(() => !handler.Requests.IsEmpty);

            Assert.Equal(TwitchTaskStatus.Waiting, task.Status);
            Assert.True(task.CanCancel);

            task.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            Assert.Equal(TwitchTaskStatus.Canceled, task.Status);
            Assert.True(task.CanReinitialize);
            Assert.Null(task.Exception);
            Assert.False(File.Exists(task.OutputFile));
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the live-status poll");
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }
    }
}
