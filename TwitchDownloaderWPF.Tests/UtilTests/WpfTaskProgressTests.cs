using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.Tests.UtilTests
{
    // Tests WpfTaskProgress, the ITaskProgress adapter between Core and every GUI page/queue task. Core reports progress thousands of times
    // per download; this class formats template statuses ("Downloading {0}%"), drops duplicate reports so the UI dispatcher is not flooded,
    // and filters log lines by the user's chosen log levels.
    public class WpfTaskProgressTests
    {
        private readonly List<int> _percents = [];
        private readonly List<string> _statuses = [];
        private readonly List<string> _logs = [];
        private readonly List<string> _ffmpegLogs = [];

        private WpfTaskProgress Create() => new(_percents.Add, _statuses.Add);

        private WpfTaskProgress Create(LogLevel logLevel, bool withFfmpegHandler = false)
        {
            Action<string>? ffmpegHandler = withFfmpegHandler ? _ffmpegLogs.Add : null;
            return new WpfTaskProgress(logLevel, _percents.Add, _statuses.Add, _logs.Add, ffmpegHandler!);
        }

        // Verifies repeated identical percentages are forwarded only once.
        // Why: Core reports per segment/comment; without de-duplication every report becomes a Dispatcher.BeginInvoke on the UI thread.
        [Fact]
        public void ReportProgress_DropsDuplicatePercent()
        {
            var progress = Create();

            progress.ReportProgress(5);
            progress.ReportProgress(5);
            progress.ReportProgress(6);

            Assert.Equal([5, 6], _percents);
        }

        // Verifies a plain status is forwarded verbatim and later percent reports do not overwrite it.
        // Why: non-template statuses (e.g. "Finalizing") contain no {0}; formatting them on each percent update would be wasted work at best.
        [Fact]
        public void SetStatus_ForwardsTextAndIsNotReformattedByProgress()
        {
            var progress = Create();

            progress.SetStatus("Finalizing");
            progress.ReportProgress(50);

            Assert.Equal(["Finalizing"], _statuses);
            Assert.Equal([50], _percents);
        }

        // Verifies a template status is formatted immediately with the initial percent and re-formatted on every new percent.
        // Why: this is how "Downloading 42%" style statuses stay current in the status bar and queue.
        [Fact]
        public void SetTemplateStatus_FormatsInitialAndSubsequentPercents()
        {
            var progress = Create();

            progress.SetTemplateStatus("Downloading {0}%", 0);
            progress.ReportProgress(10);
            progress.ReportProgress(10);

            Assert.Equal(["Downloading 0%", "Downloading 10%"], _statuses);
        }

        // Verifies switching to a new template re-emits even when the percent equals the last reported one.
        // Why: Core moves from one stage to the next at the same percent (often 0 or 100); the duplicate filter must not swallow the new
        // stage's status text, or the GUI keeps showing the previous stage.
        [Fact]
        public void SetTemplateStatus_EmitsNewTemplateAtSamePercent()
        {
            var progress = Create();

            progress.SetTemplateStatus("Downloading {0}%", 100);
            progress.SetTemplateStatus("Verifying {0}%", 100);

            Assert.Equal(["Downloading 100%", "Verifying 100%"], _statuses);
        }

        // Verifies the time-based overload formats {1}/{2} and only drops a report when percent and both times are unchanged.
        // Why: during encoding the percent often stalls while elapsed/remaining time moves; those updates must still reach the GUI.
        [Fact]
        public void TimedReportProgress_EmitsWhenOnlyTimeChanges()
        {
            var progress = Create();
            var t1 = TimeSpan.FromSeconds(1);
            var t2 = TimeSpan.FromSeconds(2);

            progress.SetTemplateStatus("{0}% {1} {2}", 10, t1, t1);
            progress.ReportProgress(10, t1, t1);
            progress.ReportProgress(10, t1, t2);

            Assert.Equal(["10% 00:00:01 00:00:01", "10% 00:00:01 00:00:02"], _statuses);
        }

        // Verifies that with the two-argument constructor (used by queue tasks) all log calls are ignored without throwing.
        // Why: queue tasks pass no log handler; a missing null check would throw NullReferenceException inside Core's download loop.
        [Fact]
        public void Logging_WithoutLogHandler_IsANoOp()
        {
            var progress = Create();

            progress.LogVerbose("v");
            progress.LogInfo("i");
            progress.LogWarning("w");
            progress.LogError("e");
            progress.LogFfmpeg("f");

            Assert.Empty(_statuses);
        }

        // Verifies only the levels enabled in the user's LogLevels setting reach the log, and errors get the localized "ERROR: " prefix.
        // Why: the Settings window lets users silence verbose/info output; ignoring the mask floods the log box, and dropping the prefix
        // makes errors indistinguishable from info lines.
        [Fact]
        public void Logging_FiltersByLevel_AndPrefixesErrors()
        {
            var progress = Create(LogLevel.Warning | LogLevel.Error);

            progress.LogVerbose("verbose");
            progress.LogInfo("info");
            progress.LogWarning("warning");
            progress.LogError("boom");

            Assert.Equal(["warning", Translations.Strings.ErrorLog + "boom"], _logs);
        }

        // Verifies the interpolated-string overloads obey the same level filter as the string overloads.
        // Why: Core calls both forms; C# picks the DefaultInterpolatedStringHandler overload for $"..." arguments, so a filter missing from
        // that overload would leak verbose lines.
        [Fact]
        public void InterpolatedLogging_FiltersByLevel()
        {
            var progress = Create(LogLevel.Info);
            var n = 3;

            progress.LogVerbose($"verbose {n}");
            progress.LogInfo($"info {n}");

            Assert.Equal(["info 3"], _logs);
        }

        // Verifies FFmpeg output is forwarded only when an FFmpeg handler is supplied, and then to that handler rather than the main log.
        // Why: the chat render page shows FFmpeg output in a separate box; other pages have none and must not get FFmpeg's very chatty output.
        [Fact]
        public void LogFfmpeg_RequiresFfmpegHandler()
        {
            Create(LogLevel.Info).LogFfmpeg("ignored");
            Create(LogLevel.Info, withFfmpegHandler: true).LogFfmpeg("frame=1");

            Assert.Equal(["frame=1"], _ffmpegLogs);
            Assert.Empty(_logs);
        }
    }
}
