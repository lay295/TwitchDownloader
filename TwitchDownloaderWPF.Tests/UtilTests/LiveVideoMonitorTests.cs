using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.Tests.TestHelpers;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.Tests.UtilTests
{
    // Tests LiveVideoMonitor, which polls Twitch's GQL API so that "delay download until the stream ends" tasks start only once the VOD
    // stops recording. It shares one poll state per video ID across all waiting tasks and tolerates transient API errors. GQL is faked;
    // each test uses its own video ID because the per-ID state is cached process-wide.
    [Collection(StaticHttpClientCollection.NAME)]
    public class LiveVideoMonitorTests
    {
        // Verifies the monitor reports "recording" while GQL says RECORDING, and exposes the response it got.
        // Why: this is the condition that keeps a delayed task waiting; DelayUntilVideoOffline also reads the thumbnail from LatestVideoResponse.
        [Fact]
        public async Task IsVideoRecording_TrueWhileStatusIsRecording()
        {
            using var _ = FakeGql.Use(FakeGql.VideoStatus("RECORDING"));
            var monitor = new LiveVideoMonitor(910_000_001);

            Assert.True(await monitor.IsVideoRecording(TestContext.Current.CancellationToken));
            Assert.Equal("RECORDING", monitor.LatestVideoResponse.data.video.status);
        }

        // Verifies the monitor reports "not recording" for any other status.
        // Why: this is what releases a waiting download; if it never turned false the task would wait forever after the stream ended.
        [Fact]
        public async Task IsVideoRecording_FalseOnceRecordingEnds()
        {
            using var _ = FakeGql.Use(FakeGql.VideoStatus("RECORDED"));
            var monitor = new LiveVideoMonitor(910_000_002);

            Assert.False(await monitor.IsVideoRecording(TestContext.Current.CancellationToken));
        }

        // Verifies two monitors for the same video share state: only one GQL request is made, and both see the same answer.
        // Why: a VOD download and its chat download both wait on the same stream; without sharing, every waiting task would poll Twitch
        // independently every few seconds.
        [Fact]
        public async Task MonitorsForSameVideo_ShareOnePoll()
        {
            var handler = FakeGql.VideoStatus("RECORDING");
            using var _ = FakeGql.Use(handler);
            var first = new LiveVideoMonitor(910_000_003);
            var second = new LiveVideoMonitor(910_000_003);

            Assert.True(await first.IsVideoRecording(TestContext.Current.CancellationToken));
            Assert.True(await second.IsVideoRecording(TestContext.Current.CancellationToken));
            Assert.Single(handler.Requests);
        }

        // Verifies a failing API keeps the video treated as live (logging a verbose retry message) until six consecutive failures,
        // after which it logs an error and treats the video as ended.
        // Why: one flaky GQL response must not start a download of a still-recording VOD, but a permanently failing API must not leave the task
        // waiting forever either.
        [Fact]
        public async Task ApiErrors_AssumeLiveUntilSixConsecutiveFailures()
        {
            using var _ = FakeGql.Use(FakeGql.VideoStatus(null));
            var logger = new RecordingTaskProgress();
            var monitor = new LiveVideoMonitor(910_000_004, logger);

            Assert.True(await monitor.IsVideoRecording(TestContext.Current.CancellationToken));
            Assert.Single(logger.Verbose);
            Assert.Empty(logger.Errors);

            // IsVideoRecording only re-polls every 30-40 seconds, so drive the remaining polls directly on the shared state.
            for (var i = 2; i <= 5; i++)
            {
                await monitor._state.CheckIsRecording(logger);
            }

            Assert.True(await monitor.IsVideoRecording(TestContext.Current.CancellationToken));
            Assert.Empty(logger.Errors);

            await monitor._state.CheckIsRecording(logger);

            Assert.False(await monitor.IsVideoRecording(TestContext.Current.CancellationToken));
            Assert.Single(logger.Errors);
        }

        // Verifies that when one poll is still in flight after 10 seconds, a second waiter on the same video does not throw.
        // Why: the VOD and chat downloads of one stream wait on the same shared state. IsVideoRecording waits up to 10 s for the shared
        // semaphore but ignores whether it got it, then always calls Release(); the second Release() overflows the semaphore
        // (SemaphoreFullException) and DelayUntilVideoOffline turns that into a Failed task.
        [Fact(Skip = "Bug: LiveVideoMonitor.IsVideoRecording ignores the result of Semaphore.WaitAsync(10s) and always calls Release(); if a GQL poll takes over 10 s, a second task waiting on the same video gets SemaphoreFullException and fails")]
        public async Task SlowPoll_SecondWaiterDoesNotThrow()
        {
            var releasePoll = new ManualResetEventSlim();
            using var _ = FakeGql.Use(new FakeHttpMessageHandler((_, _) =>
            {
                releasePoll.Wait(TimeSpan.FromSeconds(30));
                return FakeHttpMessageHandler.Json("""{"data":{"video":{"status":"RECORDING","thumbnailURLs":[]}}}""");
            }));
            var first = new LiveVideoMonitor(910_000_005);
            var second = new LiveVideoMonitor(910_000_005);

            // The fake handler blocks synchronously, so start each poll on its own thread-pool thread.
            var firstPoll = Task.Run(() => first.IsVideoRecording(CancellationToken.None));
            await Task.Delay(500, TestContext.Current.CancellationToken);
            var secondPoll = Task.Run(() => second.IsVideoRecording(CancellationToken.None));
            await Task.Delay(TimeSpan.FromSeconds(11), TestContext.Current.CancellationToken); // Let the second waiter's 10 s semaphore wait expire
            releasePoll.Set();

            Assert.True(await firstPoll);
            Assert.True(await secondPoll);
        }
    }
}
