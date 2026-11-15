using System.Diagnostics;

namespace TwitchDownloaderCore.Tests.TestHelpers
{
    /// <summary>
    /// Access to the small, synthetic sample files committed under <c>TestData/</c> (copied next to the test binary at build time),
    /// and a check for the optional ffmpeg dependency.
    /// </summary>
    public static class TestData
    {
        public static string Path(params string[] parts) => System.IO.Path.Combine([AppContext.BaseDirectory, "TestData", ..parts]);

        public static string ReadText(params string[] parts) => File.ReadAllText(Path(parts));

        private static readonly Lazy<string?> FfmpegPathLazy = new(FindFfmpeg);

        /// <summary>The full path of an ffmpeg executable on PATH, or null if ffmpeg is not installed.</summary>
        public static string? FfmpegPath => FfmpegPathLazy.Value;

        /// <summary>
        /// Skips the calling test when ffmpeg is unavailable. CI installs ffmpeg on every platform, so these tests always run there;
        /// locally they are skipped rather than failed so a missing ffmpeg doesn't look like a regression.
        /// </summary>
        public static string RequireFfmpeg()
        {
            Assert.SkipWhen(FfmpegPath is null, "ffmpeg is not installed or not on PATH");
            return FfmpegPath!;
        }

        private static string? FindFfmpeg()
        {
            var exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = System.IO.Path.Combine(dir, exe);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Runs ffmpeg with the given arguments and fails the test if it exits non-zero.</summary>
        public static void RunFfmpeg(params string[] args)
        {
            var psi = new ProcessStartInfo(RequireFfmpeg()) { RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)!;
            var stderr = process.StandardError.ReadToEndAsync();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"ffmpeg {string.Join(' ', args)} failed:\n{stderr.Result}");
        }
    }
}
