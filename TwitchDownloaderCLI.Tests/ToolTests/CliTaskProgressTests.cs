using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI.Tests.ToolTests
{
    // Tests CliTaskProgress, the ITaskProgress implementation behind every CLI verb. It decides what reaches stdout for each --log-level
    // and draws the in-place "[STATUS] - Downloading 42%" progress line. Users pipe this output into other tools, so filtering mistakes or
    // missing newlines corrupt their logs.
    // In the process-state collection because it writes to Console.Out.
    [Collection(ProcessStateCollection.NAME)]
    public class CliTaskProgressTests
    {
        private static void LogBoth(CliTaskProgress progress, LogLevel level, int value)
        {
            // Exercise both the plain string and the interpolated-string-handler overloads (LogFfmpeg only has the former).
            switch (level)
            {
                case LogLevel.Verbose: progress.LogVerbose("plain"); progress.LogVerbose($"value {value}"); break;
                case LogLevel.Info: progress.LogInfo("plain"); progress.LogInfo($"value {value}"); break;
                case LogLevel.Warning: progress.LogWarning("plain"); progress.LogWarning($"value {value}"); break;
                case LogLevel.Error: progress.LogError("plain"); progress.LogError($"value {value}"); break;
                case LogLevel.Ffmpeg: progress.LogFfmpeg("plain"); progress.LogFfmpeg($"value {value}"); break;
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        // Each log method writes "<preamble><message>" on its own line when its flag is enabled, and nothing when every other flag is
        // enabled but its own is not. Guards both the preambles users grep for and the per-level filtering.
        [Theory]
        [InlineData((int)LogLevel.Verbose, "[VERBOSE] - ")]
        [InlineData((int)LogLevel.Info, "[INFO] - ")]
        [InlineData((int)LogLevel.Warning, "[WARNING] - ")]
        [InlineData((int)LogLevel.Error, "[ERROR] - ")]
        [InlineData((int)LogLevel.Ffmpeg, "<FFMPEG> ")]
        public void LogMethods_WriteOnlyWhenTheirLevelIsEnabled(int levelValue, string preamble)
        {
            var level = (LogLevel)levelValue;
            using var console = new ConsoleCapture();

            using (var enabled = new CliTaskProgress(level))
            {
                LogBoth(enabled, level, 42);
            }

            using (var disabled = new CliTaskProgress(LogLevel.All & ~level))
            {
                LogBoth(disabled, level, 42);
            }

            Assert.Equal($"{preamble}plain\n{preamble}value 42\n", console.Output);
        }

        // --log-level None must silence everything even when combined with other flags (e.g. the legacy --silent rewrite appended to
        // an explicit level), including status lines.
        [Fact]
        public void NoneLevel_SilencesEverything()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.None | LogLevel.All))
            {
                progress.SetStatus("status");
                progress.SetTemplateStatus("{0}%", 1);
                progress.ReportProgress(2);
                progress.LogVerbose("v");
                progress.LogInfo("i");
                progress.LogWarning("w");
                progress.LogError("e");
                progress.LogFfmpeg("f");
            }

            Assert.Equal("", console.Output);
        }

        // SetStatus prints a complete status line; a later non-template ReportProgress must not reprint it (only templates take a percent).
        [Fact]
        public void SetStatus_WritesLine_AndIgnoresProgressReports()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.Status))
            {
                progress.SetStatus("Fetching Video Info [1/1]");
                progress.ReportProgress(50);
                progress.ReportProgress(50, TimeSpan.Zero, TimeSpan.Zero);
            }

            Assert.Equal("[STATUS] - Fetching Video Info [1/1]\n", console.Output);
        }

        // A template status is redrawn in place with '\r' as the percentage changes and skips redundant redraws of an unchanged percentage.
        // Disposing writes the final newline so the shell prompt doesn't land in the middle of the progress line.
        [Fact]
        public void TemplateStatus_RedrawsInPlace_AndDisposeEndsTheLine()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.Status))
            {
                progress.SetTemplateStatus("Downloading {0}%", 0);
                progress.ReportProgress(0);
                progress.ReportProgress(50);
                progress.ReportProgress(100);
            }

            Assert.Equal("[STATUS] - Downloading 0%\r[STATUS] - Downloading 50%\r[STATUS] - Downloading 100%\n", console.Output);
        }

        // A shorter redraw is padded with spaces so no stale characters of the longer previous line remain, and a log message written
        // while a progress line is open starts on a fresh line instead of being appended to the progress text.
        [Fact]
        public void TemplateStatus_PadsShorterRedraw_AndLogStartsNewLine()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.Status | LogLevel.Info))
            {
                progress.SetTemplateStatus("Item {0}", 100);
                progress.ReportProgress(5);
                progress.LogInfo("done");
            }

            Assert.Equal("[STATUS] - Item 100\r[STATUS] - Item 5  \n[INFO] - done\n", console.Output);
        }

        // Starting a new template while a progress line is open must first terminate that line, so the old status isn't overwritten.
        [Fact]
        public void NewTemplateStatus_EndsPreviousProgressLine()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.Status))
            {
                progress.SetTemplateStatus("Verifying {0}%", 100);
                progress.SetTemplateStatus("Combining {0}%", 0, TimeSpan.Zero, TimeSpan.Zero);
                progress.SetTemplateStatus("Finalizing {0}%", 0);
            }

            var lines = console.Output.Split('\n');
            Assert.Equal("[STATUS] - Verifying 100%", lines[0]);
            Assert.Contains("[STATUS] - Combining 0%", lines[1]);
            Assert.Contains("[STATUS] - Finalizing 0%", lines[2]);
        }

        // The time-aware template overload formats the percent and both times, and only redraws when one of the three values changed.
        [Fact]
        public void TemplateStatusWithTimes_FormatsAndRedrawsOnChange()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.Status))
            {
                progress.SetTemplateStatus("Rendering {0}% ({1} / {2})", 10, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
                progress.ReportProgress(10, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
                progress.ReportProgress(10, TimeSpan.FromSeconds(90), TimeSpan.FromMinutes(2));
            }

            Assert.Equal(
                "[STATUS] - Rendering 10% (00:01:00 / 00:02:00)\r[STATUS] - Rendering 10% (00:01:30 / 00:02:00)\n",
                console.Output);
        }

        // Without the Status flag, template statuses and progress reports must print nothing (e.g. `--log-level Error` for scripts).
        [Fact]
        public void StatusDisabled_SuppressesProgress()
        {
            using var console = new ConsoleCapture();

            using (var progress = new CliTaskProgress(LogLevel.All & ~LogLevel.Status))
            {
                progress.SetStatus("status");
                progress.SetTemplateStatus("{0}%", 1);
                progress.SetTemplateStatus("{0}% {1} {2}", 1, TimeSpan.Zero, TimeSpan.Zero);
                progress.ReportProgress(2);
                progress.ReportProgress(3, TimeSpan.Zero, TimeSpan.Zero);
            }

            Assert.Equal("", console.Output);
        }
    }
}
