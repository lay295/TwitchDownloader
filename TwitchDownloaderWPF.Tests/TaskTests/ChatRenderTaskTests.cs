using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF.Tests.TaskTests
{
    // Tests ChatRenderTask's dependency chaining. When the user queues "download VOD + chat + render chat", WindowQueueOptions creates the
    // render task in Waiting with DependantTask set to the chat download; the render must start only after that download finishes, and must
    // give up (not hang in Waiting forever) if it fails or is cancelled.
    public class ChatRenderTaskTests
    {
        private static ChatDownloadTask NewDependency() => new() { DownloadOptions = new ChatDownloadOptions() };

        private static ChatRenderTask NewWaitingRender(TwitchTask dependency)
        {
            var render = new ChatRenderTask { DownloadOptions = new ChatRenderOptions(), DependantTask = dependency };
            render.ChangeStatus(TwitchTaskStatus.Waiting); // As WindowQueueOptions does
            return render;
        }

        // Verifies a render with no dependency behaves like any other task: runnable when Ready, not when Waiting.
        // Why: renders queued on their own (from an existing JSON file) have no dependency and must not wait for anything.
        [Fact]
        public void CanRun_WithoutDependency_OnlyWhenReady()
        {
            var render = new ChatRenderTask { DownloadOptions = new ChatRenderOptions() };
            Assert.True(render.CanRun());

            render.ChangeStatus(TwitchTaskStatus.Waiting);
            Assert.False(render.CanRun());
        }

        // Verifies the render keeps waiting while its chat download has not finished, including while that download is stopping.
        // Why: starting the render early would parse a missing or half-written chat JSON.
        [Theory]
        [InlineData(TwitchTaskStatus.Ready)]
        [InlineData(TwitchTaskStatus.Waiting)]
        [InlineData(TwitchTaskStatus.Running)]
        [InlineData(TwitchTaskStatus.Stopping)]
        public void CanRun_KeepsWaitingWhileDependencyIsUnfinished(TwitchTaskStatus dependencyStatus)
        {
            var dependency = NewDependency();
            var render = NewWaitingRender(dependency);
            dependency.ChangeStatus(dependencyStatus);

            Assert.False(render.CanRun());
            Assert.Equal(TwitchTaskStatus.Waiting, render.Status);
        }

        // Verifies the render becomes runnable once its chat download has finished.
        // Why: this is the hand-off that makes chained queue items work at all.
        [Fact]
        public void CanRun_TrueOnceDependencyFinished()
        {
            var dependency = NewDependency();
            var render = NewWaitingRender(dependency);
            dependency.ChangeStatus(TwitchTaskStatus.Finished);

            Assert.True(render.CanRun());
        }

        // Verifies that when the chat download fails or is cancelled, the render cancels itself and offers Retry.
        // Why: otherwise the render would sit in Waiting forever (it can never run), and it would block the user from removing it, since
        // PageQueue refuses to remove non-cancellable waiting tasks.
        [Theory]
        [InlineData(TwitchTaskStatus.Failed)]
        [InlineData(TwitchTaskStatus.Canceled)]
        public void CanRun_CancelsRenderWhenDependencyFailsOrIsCanceled(TwitchTaskStatus dependencyStatus)
        {
            var dependency = NewDependency();
            var render = NewWaitingRender(dependency);
            dependency.ChangeStatus(dependencyStatus);

            Assert.False(render.CanRun());
            Assert.Equal(TwitchTaskStatus.Canceled, render.Status);
            Assert.True(render.CanReinitialize);
        }

        // Verifies retrying a chained render puts it back to Waiting (not Ready), so it waits for the retried chat download again.
        // Why: going to Ready would let the scheduler start the render immediately, before the re-downloaded chat file exists.
        [Fact]
        public void Reinitialize_WithDependency_ReturnsToWaiting()
        {
            var dependency = NewDependency();
            var render = NewWaitingRender(dependency);
            dependency.ChangeStatus(TwitchTaskStatus.Canceled);
            _ = render.CanRun();

            dependency.Reinitialize();
            render.Reinitialize();

            Assert.Equal(TwitchTaskStatus.Waiting, render.Status);
            Assert.False(render.CanRun());
        }
    }
}
