using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Tests.TestHelpers;
using Xabe.FFmpeg;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests FfmpegHandler: locating ffmpeg before a render/download (explicit path, working directory, PATH) and the `ffmpeg -d` verb.
    // If detection misses an installed ffmpeg the CLI refuses to run. Not covered: the "not found" branch (Environment.Exit) and the actual
    // download (Xabe's downloader always hits the network); the download's progress adapter is tested directly.
    // In the process-state collection because the tests change the working directory, PATH and Console.Out.
    [Collection(ProcessStateCollection.NAME)]
    public class FfmpegHandlerTests
    {
        private static string CreateFakeFfmpeg(string directory)
        {
            var path = Path.Combine(directory, FfmpegHandler.FfmpegExecutableName);
            File.WriteAllText(path, "");
            return path;
        }

        // The executable name must carry .exe on Windows only; it is used both for detection and as the default --ffmpeg-path.
        [Fact]
        public void ExecutableName_MatchesPlatform()
        {
            Assert.Equal(OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg", FfmpegHandler.FfmpegExecutableName);
        }

        // An existing --ffmpeg-path is accepted without logging an error (and without falling through to the exit path).
        [Fact]
        public void DetectFfmpeg_ExplicitPath_IsAccepted()
        {
            using var temp = new TempDirectory();
            var logger = new RecordingTaskProgress();

            FfmpegHandler.DetectFfmpeg(CreateFakeFfmpeg(temp.Path), logger);

            Assert.Empty(logger.Errors);
        }

        // ffmpeg next to the user's working directory (where `ffmpeg -d` puts it) is found even when --ffmpeg-path is not given.
        [Fact]
        public void DetectFfmpeg_InWorkingDirectory_IsAccepted()
        {
            using var temp = new TempDirectory();
            CreateFakeFfmpeg(temp.Path);
            var logger = new RecordingTaskProgress();

            using (new WorkingDirectoryOverride(temp.Path))
            {
                Assert.True(File.Exists(FfmpegHandler.FfmpegExecutableName)); // otherwise DetectFfmpeg would Environment.Exit the test host
                FfmpegHandler.DetectFfmpeg(null!, logger);
            }

            Assert.Empty(logger.Errors);
        }

        // ffmpeg installed system-wide (on PATH) is found when it is neither given explicitly nor in the working directory.
        [Fact]
        public void DetectFfmpeg_OnPath_IsAccepted()
        {
            using var temp = new TempDirectory();
            var binDir = Directory.CreateDirectory(temp.Combine("bin")).FullName;
            var emptyCwd = Directory.CreateDirectory(temp.Combine("cwd")).FullName;
            CreateFakeFfmpeg(binDir);
            var logger = new RecordingTaskProgress();

            using (new EnvironmentVariableOverride(binDir, "PATH"))
            using (new WorkingDirectoryOverride(emptyCwd))
            {
                Assert.True(PathUtils.ExistsOnPATH(FfmpegHandler.FfmpegExecutableName)); // otherwise DetectFfmpeg would Environment.Exit the test host
                FfmpegHandler.DetectFfmpeg(temp.Combine("missing", "ffmpeg"), logger);
            }

            Assert.Empty(logger.Errors);
        }

        // `ffmpeg` without -d must do nothing (no download, no output).
        [Fact]
        public void ParseArgs_WithoutDownload_DoesNothing()
        {
            using var console = new ConsoleCapture();

            FfmpegHandler.ParseArgs(CliArgs.Parse<FfmpegArgs>("ffmpeg"));

            Assert.Equal("", console.Output);
        }

        // `ffmpeg -d` must not re-download (or overwrite) an ffmpeg that already sits in the working directory.
        [Fact]
        public void ParseArgs_Download_SkipsWhenAlreadyPresent()
        {
            using var temp = new TempDirectory();
            CreateFakeFfmpeg(temp.Path);
            using var cwd = new WorkingDirectoryOverride(temp.Path);
            Assert.True(File.Exists(FfmpegHandler.FfmpegExecutableName)); // otherwise the verb would start a real download
            using var console = new ConsoleCapture();

            FfmpegHandler.ParseArgs(CliArgs.Parse<FfmpegArgs>("ffmpeg", "--download"));

            Assert.Equal("[INFO] - FFmpeg was already found in the current working directory.\n", console.Output);
            Assert.Equal("", File.ReadAllText(FfmpegHandler.FfmpegExecutableName));
        }

        // The download progress adapter starts a "Downloading FFmpeg" template status, converts bytes to a percentage and forwards it
        // from its timer thread, ignoring reports that would move the percentage backwards.
        [Fact]
        public async Task XabeProgressHandler_ForwardsPercentages()
        {
            var progress = new RecordingTaskProgress();
            var handler = new FfmpegHandler.XabeProgressHandler(progress);
            var reporter = (IProgress<ProgressInfo>)handler;

            using ((IDisposable)handler)
            {
                reporter.Report(new ProgressInfo(25, 100));
                reporter.Report(new ProgressInfo(10, 100)); // lower than before: ignored
                reporter.Report(new ProgressInfo(50, 100));

                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!progress.Percentages.Contains(50) && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(20, TestContext.Current.CancellationToken);
                }
            }

            Assert.Equal(["Downloading FFmpeg {0}%"], progress.Statuses);
            Assert.Contains(50, progress.Percentages);
            Assert.DoesNotContain(10, progress.Percentages);
        }
    }
}
