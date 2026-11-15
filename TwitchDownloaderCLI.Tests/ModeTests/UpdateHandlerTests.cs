using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests UpdateHandler, the `update` verb that checks the update feed and replaces the CLI executable in place. A broken version check
    // nags or never updates; a broken install step can leave users without a working binary.
    // The feed and archive downloads are faked through UpdateHandler.HttpClient. AutoUpdate itself is NOT run because it operates on
    // Environment.ProcessPath (the test host); its building blocks (download, backup, extract, permissions) are tested on temp files instead.
    // In the static-HttpClient collection (non-parallel), which also makes it safe to redirect Console.In/Out for the confirmation prompt.
    [Collection(StaticHttpClientCollection.NAME)]
    public class UpdateHandlerTests
    {
        private const string UPDATE_FEED_URL = "https://downloader-update.twitcharchives.workers.dev/";

        private static string UpdateXml(string version) =>
            $"<item><version>{version}</version><url-cli>https://example.invalid/releases/TwitchDownloaderCLI-{version}-{{0}}.zip</url-cli></item>";

        /// <summary>Serves <paramref name="feed"/> for the update feed URL and 404 for everything else, so no archive can ever be downloaded.</summary>
        private static FakeHttpMessageHandler FakeFeed(string feed) => new((request, _) => request.RequestUri!.AbsoluteUri == UPDATE_FEED_URL
            ? FakeHttpMessageHandler.Text(feed)
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        private static HttpClientOverride OverrideHttp(HttpMessageHandler handler) => new(c => UpdateHandler.HttpClient = c, () => UpdateHandler.HttpClient, handler);

        // A feed version that is not newer than the running CLI (1.56.5) reports "latest version" and downloads nothing.
        [Theory]
        [InlineData("1.0.0")]
        [InlineData("1.56.5")]
        public async Task CheckForUpdate_NotNewer_ReportsLatest(string feedVersion)
        {
            var http = FakeFeed(UpdateXml(feedVersion));
            var progress = new RecordingTaskProgress();

            using (OverrideHttp(http))
            {
                await UpdateHandler.CheckForUpdate(new UpdateArgs(), progress);
            }

            Assert.Equal(["You have the latest version of TwitchDownloaderCLI"], progress.Info);
            Assert.Equal(UPDATE_FEED_URL, Assert.Single(http.Requests).Uri.AbsoluteUri);
        }

        // An empty feed response must be reported as an error instead of crashing in the XML parser.
        [Fact]
        public async Task CheckForUpdate_EmptyFeed_LogsError()
        {
            var progress = new RecordingTaskProgress();

            using (OverrideHttp(FakeFeed("  ")))
            {
                await UpdateHandler.CheckForUpdate(new UpdateArgs(), progress);
            }

            Assert.Equal(["Could not parse remote update info XML"], progress.Errors);
            Assert.Empty(progress.Info);
        }

        // A newer version is announced, the platform-specific package URL is built from the feed's template, and the user is asked before
        // anything is downloaded; answering "no" must leave everything untouched (no archive request).
        [Fact]
        public async Task CheckForUpdate_NewerVersion_DeclinedPromptDownloadsNothing()
        {
            var packageName = UpdateHandler.ConstructPackageName("TwitchDownloaderCLI-999.0.0-{0}.zip");
            Assert.SkipWhen(packageName is null, "Self-update is not supported on this OS/architecture");
            var http = FakeFeed(UpdateXml("999.0.0"));
            var progress = new RecordingTaskProgress();

            using (OverrideHttp(http))
            using (var console = new ConsoleCapture("n\n"))
            {
                await UpdateHandler.CheckForUpdate(new UpdateArgs(), progress);

                Assert.StartsWith("Would you like to update?", console.Output);
            }

            Assert.Equal(["TwitchDownloaderCLI v999.0.0 is available!"], progress.Info);
            Assert.Contains($"Constructed download URL: https://example.invalid/releases/{packageName}", progress.Verbose);
            Assert.Single(http.Requests);
        }

        // The package name must match the release asset naming for the current OS/architecture; a mismatch makes self-update 404.
        [Fact]
        public void ConstructPackageName_MatchesReleaseAssetNames()
        {
            var arch = RuntimeInformation.OSArchitecture;
            var isMusl = RuntimeInformation.RuntimeIdentifier.Contains("musl");
            string? expected = (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), OperatingSystem.IsLinux(), arch) switch
            {
                (true, _, _, Architecture.X64) => "Windows-x64",
                (_, true, _, Architecture.X64) => "MacOS-x64",
                (_, true, _, Architecture.Arm64) => "MacOSArm64",
                (_, _, true, Architecture.X64) => isMusl ? "LinuxAlpine-x64" : "Linux-x64",
                (_, _, true, Architecture.Arm) when !isMusl => "LinuxArm",
                (_, _, true, Architecture.Arm64) when !isMusl => "LinuxArm64",
                _ => null
            };

            var actual = UpdateHandler.ConstructPackageName("TwitchDownloaderCLI-1.0.0-{0}.zip");

            Assert.Equal(expected is null ? null : $"TwitchDownloaderCLI-1.0.0-{expected}.zip", actual);
        }

        // The update archive is streamed to disk byte for byte and progress ends at 100%.
        [Fact]
        public async Task DownloadUpdateArchive_WritesFile()
        {
            using var temp = new TempDirectory();
            var payload = Enumerable.Range(0, 10_000).Select(i => (byte)i).ToArray();
            var progress = new RecordingTaskProgress();
            var archivePath = temp.Combine("update.zip");

            using (OverrideHttp(new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Bytes(payload))))
            {
                await UpdateHandler.DownloadUpdateArchive("https://example.invalid/update.zip", progress, archivePath);
            }

            Assert.Equal(payload, await File.ReadAllBytesAsync(archivePath, TestContext.Current.CancellationToken));
            Assert.Equal(100, progress.Percentages.Last());
        }

        // A failed download (e.g. 404 for an unknown package) must throw before any file is created, so a half-written archive is never
        // extracted over the executable.
        [Fact]
        public async Task DownloadUpdateArchive_HttpError_ThrowsWithoutCreatingFile()
        {
            using var temp = new TempDirectory();
            var archivePath = temp.Combine("update.zip");

            using (OverrideHttp(FakeHttpMessageHandler.NotFound()))
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => UpdateHandler.DownloadUpdateArchive("https://example.invalid/update.zip", new RecordingTaskProgress(), archivePath));
            }

            Assert.False(File.Exists(archivePath));
        }

        // Extraction writes every archive entry into the install directory, overwriting the existing files, and reports progress to 100%.
        [Fact]
        public async Task ExtractZipFiles_OverwritesExistingFiles()
        {
            using var temp = new TempDirectory();
            var archivePath = temp.Combine("update.zip");
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                await using (var writer = new StreamWriter(zip.CreateEntry("TwitchDownloaderCLI").Open())) await writer.WriteAsync("new binary");
                await using (var writer = new StreamWriter(zip.CreateEntry("COPYRIGHT.txt").Open())) await writer.WriteAsync("license");
            }

            var installDir = Directory.CreateDirectory(temp.Combine("install")).FullName;
            await File.WriteAllTextAsync(Path.Combine(installDir, "TwitchDownloaderCLI"), "old binary", TestContext.Current.CancellationToken);
            var progress = new RecordingTaskProgress();

            await UpdateHandler.ExtractZipFiles(progress, archivePath, installDir);

            Assert.Equal("new binary", await File.ReadAllTextAsync(Path.Combine(installDir, "TwitchDownloaderCLI"), TestContext.Current.CancellationToken));
            Assert.Equal("license", await File.ReadAllTextAsync(Path.Combine(installDir, "COPYRIGHT.txt"), TestContext.Current.CancellationToken));
            Assert.Equal([50, 100, 100], progress.Percentages);
        }

        // Before extracting, the current executable is moved to "<exe>.bak" (replacing an older backup) so a failed update can be undone.
        [Fact]
        public void BackupCurrentExecutable_MovesExeToBak()
        {
            using var temp = new TempDirectory();
            var exe = temp.Combine("TwitchDownloaderCLI");
            File.WriteAllText(exe, "current");
            File.WriteAllText(exe + ".bak", "older");
            var progress = new RecordingTaskProgress();

            UpdateHandler.BackupCurrentExecutable(exe, progress);

            Assert.False(File.Exists(exe));
            Assert.Equal("current", File.ReadAllText(exe + ".bak"));
            Assert.Single(progress.Verbose);
        }

        // A backup failure is only a warning: the update can still proceed.
        [Fact]
        public void BackupCurrentExecutable_Failure_OnlyWarns()
        {
            using var temp = new TempDirectory();
            var progress = new RecordingTaskProgress();

            UpdateHandler.BackupCurrentExecutable(temp.Combine("missing"), progress);

            Assert.Contains("Failed to create backup", Assert.Single(progress.Warnings));
        }

        // On Linux/macOS the executable bit must survive the update: the old file mode is read before and re-applied after extraction.
        [Fact]
        [UnsupportedOSPlatform("windows")]
        public void UnixFilePermissions_AreCapturedAndRestored()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes do not exist on Windows");
            using var temp = new TempDirectory();
            var exe = temp.Combine("TwitchDownloaderCLI");
            File.WriteAllText(exe, "");
            const UnixFileMode MODE = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
            File.SetUnixFileMode(exe, MODE);
            var progress = new RecordingTaskProgress();

            var captured = UpdateHandler.GetUnixFilePermissions(progress, exe);
            File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite); // extraction resets the mode
            UpdateHandler.ApplyUnixFilePermissions(progress, exe, captured);

            Assert.Equal(MODE, captured);
            Assert.Equal(MODE, File.GetUnixFileMode(exe));
            Assert.Empty(progress.Errors);
        }

        // Failing to restore permissions is reported with a chmod hint instead of aborting the update, and without a captured mode nothing
        // is applied. (The read-failure branch is unreachable here: FileInfo.UnixFileMode returns -1 for a missing file instead of throwing.)
        [Fact]
        [UnsupportedOSPlatform("windows")]
        public void UnixFilePermissions_RestoreFailureIsLogged()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes do not exist on Windows");
            using var temp = new TempDirectory();
            var missing = temp.Combine("missing");
            var progress = new RecordingTaskProgress();

            UpdateHandler.ApplyUnixFilePermissions(progress, missing, null);
            Assert.Empty(progress.Errors);

            UpdateHandler.ApplyUnixFilePermissions(progress, missing, UnixFileMode.UserRead);
            var chmod = OperatingSystem.IsMacOS() ? "chmod +x" : "sudo chmod +x";
            Assert.Contains($"Please run '{chmod} missing'", Assert.Single(progress.Errors));
        }

        // On Windows there are no Unix modes: nothing is captured and applying is a no-op.
        [Fact]
        public void UnixFilePermissions_AreIgnoredOnWindows()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Only applies to Windows");
            using var temp = new TempDirectory();
            var exe = temp.Combine("TwitchDownloaderCLI.exe");
            File.WriteAllText(exe, "");
            var progress = new RecordingTaskProgress();

            Assert.Null(UpdateHandler.GetUnixFilePermissions(progress, exe));
            UpdateHandler.ApplyUnixFilePermissions(progress, exe, UnixFileMode.UserRead);
            Assert.Empty(progress.Errors);
        }

        // The verb entry point: debug builds refuse to self-update, release builds run the update check (here against the fake feed).
        [Fact]
        public void ParseArgs_RunsUpdateCheckOnlyInReleaseBuilds()
        {
            using var http = OverrideHttp(FakeFeed(UpdateXml("1.0.0")));
            using var console = new ConsoleCapture();

            UpdateHandler.ParseArgs(CliArgs.Parse<UpdateArgs>("update"));

#if DEBUG
            Assert.Equal("[INFO] - Self-update is not supported for debug builds\n", console.Output);
#else
            Assert.Equal("[INFO] - You have the latest version of TwitchDownloaderCLI\n", console.Output);
#endif
        }
    }
}
