using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests DriveHelper, which finds the drive a file is written to (for the VOD free-space warning) and waits for slow drives
    // during chat renders. These tests use the real drives of the machine, so they only assert machine-independent properties.
    public class DriveHelperTests
    {
        // The drive whose root is the longest prefix of the path, i.e. the mount point the file really lives on.
        private static DriveInfo? ExpectedDrive(string path) => DriveInfo.GetDrives()
            .Where(d => path.StartsWith(d.Name, StringComparison.Ordinal))
            .MaxBy(d => d.Name.Length);

        /// <summary>A drive is always returned (never null or an exception) for a normal output path, so the free-space check cannot crash a download.</summary>
        [Fact]
        public void GetOutputDrive_ReturnsAKnownDrive()
        {
            var path = Path.Combine(Path.GetTempPath(), "video.mp4");

            var drive = DriveHelper.GetOutputDrive(path);

            Assert.Contains(drive.Name, DriveInfo.GetDrives().Select(d => d.Name));
        }

        /// <summary>
        /// The returned drive should be the mount point containing the path (longest matching root). The search starts from
        /// GetDrives()[0] even when that drive does not contain the path, and then skips any matching drive with a shorter name.
        /// On Linux the first mount is often /proc or /sys, so a file under "/" is reported as being on /proc (0 bytes free),
        /// producing bogus "not enough free space" warnings.
        /// </summary>
        [Fact(Skip = "Bug: GetOutputDrive starts from GetDrives()[0] and skips shorter matching roots, so on Linux it can return /proc instead of /")]
        public void GetOutputDrive_ReturnsMountPointContainingPath()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("video.mp4");

            Assert.Equal(ExpectedDrive(path)?.Name, DriveHelper.GetOutputDrive(path).Name);
        }

        /// <summary>A drive that is already ready returns immediately without logging "Waiting for output drive".</summary>
        [Fact]
        public void WaitForDrive_ReturnsImmediatelyForReadyDrive()
        {
            var drive = ExpectedDrive(Path.GetFullPath(Path.GetTempPath()));
            Assert.SkipWhen(drive is not { IsReady: true }, "No ready drive contains the temp directory");
            var progress = new RecordingTaskProgress();

            DriveHelper.WaitForDrive(drive!, progress);

            Assert.Empty(progress.Info);
        }

        /// <summary>
        /// A drive that comes back (e.g. a USB disk waking up) is waited for, with a "Waiting for output drive" status logged every 250 ms,
        /// and the render resumes as soon as it is ready. On Unix, DriveInfo.IsReady is "the mount directory exists", which lets the
        /// test bring a drive online on demand; Windows drive letters cannot be simulated, so the test is skipped there.
        /// </summary>
        [Fact]
        public void WaitForDrive_WaitsAndLogsUntilDriveIsReady()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "A not-ready drive cannot be simulated on Windows");
            using var temp = new TempDirectory();
            var mountPoint = temp.Combine("usb");
            var drive = new DriveInfo(mountPoint);
            Assert.False(drive.IsReady);
            var progress = new RecordingTaskProgress();

            using var timer = new Timer(_ => Directory.CreateDirectory(mountPoint), null, 400, Timeout.Infinite);
            DriveHelper.WaitForDrive(drive, progress);

            Assert.True(drive.IsReady);
            Assert.NotEmpty(progress.Info);
            Assert.All(progress.Info, x => Assert.StartsWith("Waiting for output drive (", x));
        }
    }
}
