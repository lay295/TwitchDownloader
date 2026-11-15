using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // Tests StubTaskProgress, the do-nothing ITaskProgress that background helpers (e.g. HighlightIcons image fetches) log to.
    public class StubTaskProgressTests
    {
        /// <summary>
        /// The shared instance must accept every ITaskProgress call without throwing (including interpolated-string overloads),
        /// so code paths that log into the stub can never crash a render or download.
        /// </summary>
        [Fact]
        public void EveryMemberIsANoOp()
        {
            var progress = StubTaskProgress.Instance;
            var value = 42;

            progress.LogVerbose("verbose");
            progress.LogVerbose($"verbose {value}");
            progress.LogInfo("info");
            progress.LogInfo($"info {value}");
            progress.LogWarning("warning");
            progress.LogWarning($"warning {value}");
            progress.LogError("error");
            progress.LogError($"error {value}");
            progress.LogFfmpeg("ffmpeg");
            progress.SetStatus("status");
            progress.SetTemplateStatus("status {0}", 0);
            progress.SetTemplateStatus("status {0} {1} {2}", 0, TimeSpan.Zero, TimeSpan.Zero);
            progress.ReportProgress(50);
            progress.ReportProgress(50, TimeSpan.Zero, TimeSpan.Zero);

            Assert.Same(StubTaskProgress.Instance, progress);
        }
    }
}
