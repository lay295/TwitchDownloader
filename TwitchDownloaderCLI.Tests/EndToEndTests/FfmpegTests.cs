using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of how the CLI finds ffmpeg (FfmpegHandler.DetectFfmpeg: --ffmpeg-path, then the working directory, then PATH)
    // and of the `ffmpeg` verb's offline path. The CLI is run with PATH pointing at an empty folder so a system-wide ffmpeg cannot leak in.
    // Guards the user-facing "Unable to find FFmpeg" error and the --ffmpeg-path escape hatch.
    public class FfmpegTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private readonly Dictionary<string, string?> _noFfmpegOnPath;
        private static readonly string ChatJson = TestData.Path("E2E", "chat.json");
        private static readonly string FfmpegFileName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        public FfmpegTests()
        {
            _noFfmpegOnPath = new() { ["PATH"] = Directory.CreateDirectory(_temp.Combine("emptybin")).FullName };
        }

        public void Dispose() => _temp.Dispose();

        // Every verb that needs ffmpeg must check for it up front and fail with a clear message and exit code 1, before contacting
        // Twitch or creating the output file (so a half-written file is never left behind).
        [Theory]
        [InlineData("chatrender", "-i", "{chat}", "-o", "{out}")]
        [InlineData("videodownload", "--id", "123456789", "-o", "{out}")]
        [InlineData("clipdownload", "--id", "SomeClipSlug", "-o", "{out}")]
        public void VerbsFailClearlyWhenFfmpegIsMissing(params string[] args)
        {
            var output = _temp.Combine("out.mp4");
            args = args.Select(a => a.Replace("{chat}", ChatJson).Replace("{out}", output)).ToArray();

            var result = Cli.Run(_temp.Path, args, env: _noFfmpegOnPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("[ERROR] - Unable to find FFmpeg, exiting.", result.StdOut);
            Assert.Contains("ffmpeg -d", result.StdOut);
            Assert.False(File.Exists(output));
        }

        // --ffmpeg-path must be enough on its own: with no ffmpeg on PATH or in the working directory the render must still succeed.
        [Fact]
        public void ExplicitFfmpegPathIsUsed()
        {
            var ffmpeg = TestData.RequireFfmpeg();
            var output = _temp.Combine("render.mp4");

            var result = Cli.Run(_temp.Path, ["chatrender", "-i", ChatJson, "-o", output, "--offline", "-w", "100", "-h", "100", "--framerate", "10", "--emoji-vendor", "none", "--ffmpeg-path", ffmpeg], env: _noFfmpegOnPath);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.True(new FileInfo(output).Length > 0);
        }

        // `ffmpeg -d` must not download (or overwrite) anything when an ffmpeg executable is already in the working directory.
        [Fact]
        public void DownloadIsSkippedWhenFfmpegIsInWorkingDirectory()
        {
            var existing = _temp.Combine(FfmpegFileName);
            File.WriteAllText(existing, "placeholder");

            var result = Cli.RunOk(_temp.Path, "ffmpeg", "-d");

            Assert.Contains("FFmpeg was already found in the current working directory.", result.StdOut);
            Assert.Equal("placeholder", File.ReadAllText(existing));
        }
    }
}
