using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Tests.TestHelpers;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF.Tests.TaskTests
{
    // Tests TwitchTask, the base of every queue item (ChatDownload/ChatRender/ChatUpdate/ClipDownload/VodDownload tasks), through its
    // concrete subclasses. PageQueue's scheduler and buttons are driven entirely by these properties: CanRun decides what starts,
    // CanCancel/CanReinitialize decide which buttons are shown, and Status/DisplayStatus/StatusImage are what the user sees.
    // The Core worker is either never started (cancel-before-run) or fails offline within milliseconds (see TestTasks).
    // It temporarily changes Settings.Default.ReduceMotion, so it runs in the non-parallel collection.
    [Collection(ProcessStateCollection.NAME)]
    public sealed class TwitchTaskTests : IDisposable
    {
        private readonly bool _originalReduceMotion = Settings.Default.ReduceMotion;
        private readonly TempDirectory _tempDir = new();

        public static TheoryData<string> AllTypes => TestTasks.AllTypes;

        public TwitchTaskTests()
        {
            Settings.Default.ReduceMotion = false;
        }

        public void Dispose()
        {
            Settings.Default.ReduceMotion = _originalReduceMotion;
            _tempDir.Dispose();
        }

        private static ClipDownloadTask NewTask() => new() { DownloadOptions = new ClipDownloadOptions() };

        // Verifies every task type starts Ready, runnable, with no progress, error or retry option.
        // Why: PageQueue's scheduler only starts tasks whose CanRun() is true; a new task that is not runnable would sit in the queue forever.
        [Theory]
        [MemberData(nameof(AllTypes))]
        public void NewTask_IsReadyAndRunnable(string type)
        {
            var task = TestTasks.CreateOfflineFailing(type, _tempDir);

            Assert.Equal(TwitchTaskStatus.Ready, task.Status);
            Assert.True(task.CanRun());
            Assert.Equal(0, task.Progress);
            Assert.Null(task.Exception);
            Assert.False(task.CanReinitialize);
        }

        // Verifies OutputFile reports the path from the task's own options object.
        // Why: the queue's "Open folder" and "Copy path" actions use OutputFile; each subclass maps it to a differently named option
        // (Filename vs OutputFile), and pointing at the wrong one opens the wrong folder or copies an empty path.
        [Theory]
        [MemberData(nameof(AllTypes))]
        public void OutputFile_IsTheConfiguredOutputPath(string type)
        {
            var task = TestTasks.CreateOfflineFailing(type, _tempDir);

            Assert.Equal(TestTasks.OutputPath(_tempDir, type), task.OutputFile);
        }

        // Verifies a freshly queued task shows its status and a Cancel button, exactly like a task that was reset to Ready by Retry.
        // Why: PageQueue binds the Cancel button to CanCancel and the status text to DisplayStatus. New tasks never call ChangeStatus, so they
        // keep the defaults (CanCancel = false, DisplayStatus = null): while waiting for a free slot they show "Status: " and cannot be cancelled.
        [Fact(Skip = "Existing behavior in v1.56.5: a newly constructed TwitchTask has Status Ready but CanCancel == false and DisplayStatus == null until ChangeStatus is first called, so queued tasks show an empty status and no Cancel button")]
        public void NewTask_IsCancellableAndShowsStatus()
        {
            var task = NewTask();

            Assert.True(task.CanCancel);
            Assert.Equal(nameof(TwitchTaskStatus.Ready), task.DisplayStatus);
        }

        // Verifies ChangeStatus mirrors the status into DisplayStatus and allows cancelling only in Waiting, Ready and Running.
        // Why: CanCancel drives the Cancel button; offering it on Finished/Failed/Canceled tasks or while already Stopping would let the user
        // cancel a token that is already disposed or cancelled.
        [Theory]
        [InlineData(TwitchTaskStatus.Waiting, true)]
        [InlineData(TwitchTaskStatus.Ready, true)]
        [InlineData(TwitchTaskStatus.Running, true)]
        [InlineData(TwitchTaskStatus.Stopping, false)]
        [InlineData(TwitchTaskStatus.Failed, false)]
        [InlineData(TwitchTaskStatus.Finished, false)]
        [InlineData(TwitchTaskStatus.Canceled, false)]
        public void ChangeStatus_SetsDisplayStatusAndCancellability(TwitchTaskStatus status, bool canCancel)
        {
            var task = NewTask();

            task.ChangeStatus(status);

            Assert.Equal(status, task.Status);
            Assert.Equal(status.ToString(), task.DisplayStatus);
            Assert.Equal(canCancel, task.CanCancel);
        }

        // Verifies the animated status image chosen for each status.
        // Why: the queue shows these gifs next to each task; finished/failed/canceled tasks must stop animating.
        [Theory]
        [InlineData(TwitchTaskStatus.Waiting, "Images/ppHop.gif")]
        [InlineData(TwitchTaskStatus.Ready, "Images/ppHop.gif")]
        [InlineData(TwitchTaskStatus.Running, "Images/ppOverheat.gif")]
        [InlineData(TwitchTaskStatus.Stopping, "Images/ppStretch.gif")]
        [InlineData(TwitchTaskStatus.Failed, null)]
        [InlineData(TwitchTaskStatus.Finished, null)]
        [InlineData(TwitchTaskStatus.Canceled, null)]
        public void ChangeStatus_PicksStatusImage(TwitchTaskStatus status, string? expectedImage)
        {
            var task = NewTask();

            task.ChangeStatus(status);

            Assert.Equal(expectedImage, task.StatusImage);
        }

        // Verifies no status image is shown for any status when the Reduce Motion setting is on.
        // Why: Reduce Motion is an accessibility setting; any animated gif slipping through defeats it.
        [Theory]
        [InlineData(TwitchTaskStatus.Waiting)]
        [InlineData(TwitchTaskStatus.Running)]
        [InlineData(TwitchTaskStatus.Stopping)]
        public void ChangeStatus_ShowsNoImageWithReduceMotion(TwitchTaskStatus status)
        {
            Settings.Default.ReduceMotion = true;
            var task = NewTask();

            task.ChangeStatus(status);

            Assert.Null(task.StatusImage);
        }

        // Verifies PropertyChanged fires for exactly the properties whose value changed, and not at all when re-applying the same status.
        // Why: the queue ListView refreshes from these notifications; a missing one leaves stale status text, and redundant ones cause
        // needless UI work for every task on every scheduler tick.
        [Fact]
        public void ChangeStatus_NotifiesOnlyChangedProperties()
        {
            var task = NewTask();
            task.ChangeStatus(TwitchTaskStatus.Ready);
            var changed = new List<string?>();
            task.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            task.ChangeStatus(TwitchTaskStatus.Running);
            task.ChangeStatus(TwitchTaskStatus.Running);

            Assert.Equal([nameof(TwitchTask.Status), nameof(TwitchTask.DisplayStatus), nameof(TwitchTask.StatusImage)], changed);
        }

        // Verifies Cancel on a task that is not cancellable (here Finished) changes nothing.
        // Why: the context menu can invoke Cancel on any task; a finished task must not flip to Canceled and lose its result state.
        [Fact]
        public void Cancel_WhenNotCancellable_DoesNothing()
        {
            var task = NewTask();
            task.ChangeStatus(TwitchTaskStatus.Finished);

            task.Cancel();

            Assert.Equal(TwitchTaskStatus.Finished, task.Status);
        }

        // Verifies cancelling a task that has not started goes straight to Canceled, while cancelling a running task goes to Stopping.
        // Why: a running task needs time to unwind (delete partial files) and finishes the transition to Canceled itself; a task that never
        // started has nothing to unwind. Both must stop offering Cancel so it cannot be pressed twice.
        [Theory]
        [InlineData(TwitchTaskStatus.Ready, TwitchTaskStatus.Canceled)]
        [InlineData(TwitchTaskStatus.Waiting, TwitchTaskStatus.Canceled)]
        [InlineData(TwitchTaskStatus.Running, TwitchTaskStatus.Stopping)]
        public void Cancel_MovesToCanceledOrStopping(TwitchTaskStatus from, TwitchTaskStatus expected)
        {
            var task = NewTask();
            task.ChangeStatus(from);

            task.Cancel();

            Assert.Equal(expected, task.Status);
            Assert.False(task.CanCancel);
        }

        // Verifies a task cancelled before RunAsync ends Canceled and retryable without starting its Core worker.
        // Why: the scheduler may call RunAsync on a task the user cancelled moments earlier; it must not start downloading anyway,
        // and it must offer Retry. The output file not being created proves the worker never ran.
        [Theory]
        [MemberData(nameof(AllTypes))]
        public async Task RunAsync_AfterCancel_EndsCanceledWithoutWork(string type)
        {
            var task = TestTasks.CreateOfflineFailing(type, _tempDir);
            File.Delete(task.OutputFile);
            task.ChangeStatus(TwitchTaskStatus.Ready);
            task.Cancel();

            await task.RunAsync();

            Assert.Equal(TwitchTaskStatus.Canceled, task.Status);
            Assert.True(task.CanReinitialize);
            Assert.Null(task.Exception);
            Assert.False(File.Exists(task.OutputFile));
        }

        // Verifies an exception from the Core worker ends the task Failed with the exception stored, Retry offered, and Cancel hidden.
        // Why: the queue's error button shows task.Exception; an exception escaping RunAsync instead would be unobserved (the scheduler
        // fire-and-forgets RunAsync) and leave the task stuck as Running forever.
        [Theory]
        [MemberData(nameof(AllTypes))]
        public async Task RunAsync_WhenWorkerFails_EndsFailedAndRetryable(string type)
        {
            var task = TestTasks.CreateOfflineFailing(type, _tempDir);

            await task.RunIgnoringGcCollectBugAsync();

            Assert.Equal(TwitchTaskStatus.Failed, task.Status);
            Assert.NotNull(task.Exception);
            Assert.True(task.CanReinitialize);
            Assert.False(task.CanCancel);
        }

        // Verifies Retry (Reinitialize) after a cancel resets progress/error state and gives the task a fresh cancellation token, so the
        // next run actually executes (here reaching the offline failure) instead of being cancelled again immediately.
        // Why: RunAsync disposes the token source; if Reinitialize reused it, every retry of a cancelled task would end Canceled instantly
        // (or throw ObjectDisposedException).
        [Theory]
        [MemberData(nameof(AllTypes))]
        public async Task Reinitialize_AfterCancel_LetsTheTaskRunAgain(string type)
        {
            var task = TestTasks.CreateOfflineFailing(type, _tempDir);
            task.ChangeStatus(TwitchTaskStatus.Ready);
            task.Cancel();
            await task.RunAsync();

            task.Reinitialize();

            Assert.Equal(TwitchTaskStatus.Ready, task.Status);
            Assert.True(task.CanRun());
            Assert.True(task.CanCancel);
            Assert.False(task.CanReinitialize);
            Assert.Equal(0, task.Progress);
            Assert.Null(task.Exception);

            await task.RunIgnoringGcCollectBugAsync();

            Assert.Equal(TwitchTaskStatus.Failed, task.Status);
        }

        // Verifies RunAsync completes without throwing once the task has reached a final state (here Failed).
        // Why: every RunAsync that runs to its end finishes with GC.Collect(-1, ...), and .NET rejects a negative generation with
        // ArgumentOutOfRangeException. The status is already set by then and PageQueue never awaits RunAsync, so users don't see it, but the
        // intended post-task collection never happens and every finished/failed task leaves an unobserved faulted Task behind.
        [Theory(Skip = "Bug: every TwitchTask.RunAsync that reaches its end throws ArgumentOutOfRangeException from GC.Collect(-1, GCCollectionMode.Default, false) (generation must be >= 0), so the intended collection never runs and the returned Task is faulted")]
        [MemberData(nameof(AllTypes))]
        public async Task RunAsync_CompletesWithoutThrowing(string type)
        {
            var task = TestTasks.CreateOfflineFailing(type, _tempDir);

            await task.RunAsync();

            Assert.Equal(TwitchTaskStatus.Failed, task.Status);
        }
    }
}
