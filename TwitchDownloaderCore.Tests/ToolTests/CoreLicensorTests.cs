using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests CoreLicensor, which keeps COPYRIGHT.txt and THIRD-PARTY-LICENSES.txt next to the app (a license obligation).
    public class CoreLicensorTests
    {
        private static readonly (string FileName, byte[] Content)[] LicenseFiles =
        [
            ("COPYRIGHT.txt", Properties.Resources.copyright),
            ("THIRD-PARTY-LICENSES.txt", Properties.Resources.third_party_licenses),
        ];

        /// <summary>Both license files are created with exactly the embedded resource content.</summary>
        [Fact]
        public void CreatesMissingLicenseFiles()
        {
            using var temp = new TempDirectory();

            CoreLicensor.EnsureFilesExist(temp.Path);

            foreach (var (fileName, content) in LicenseFiles)
            {
                Assert.Equal(content, File.ReadAllBytes(temp.Combine(fileName)));
            }
        }

        /// <summary>A tampered or truncated license file is restored to the embedded content on the next start.</summary>
        [Fact]
        public void RestoresModifiedLicenseFiles()
        {
            using var temp = new TempDirectory();
            foreach (var (fileName, _) in LicenseFiles)
            {
                File.WriteAllText(temp.Combine(fileName), "tampered");
            }

            CoreLicensor.EnsureFilesExist(temp.Path);

            foreach (var (fileName, content) in LicenseFiles)
            {
                Assert.Equal(content, File.ReadAllBytes(temp.Combine(fileName)));
            }
        }

        /// <summary>Up-to-date files are left untouched (hash comparison), so starting the app does not rewrite files every time.</summary>
        [Fact]
        public void LeavesIdenticalFilesUntouched()
        {
            using var temp = new TempDirectory();
            CoreLicensor.EnsureFilesExist(temp.Path);
            var oldTime = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            foreach (var (fileName, _) in LicenseFiles)
            {
                File.SetLastWriteTimeUtc(temp.Combine(fileName), oldTime);
            }

            CoreLicensor.EnsureFilesExist(temp.Path);

            foreach (var (fileName, _) in LicenseFiles)
            {
                Assert.Equal(oldTime, File.GetLastWriteTimeUtc(temp.Combine(fileName)));
            }
        }

        /// <summary>
        /// I/O failures (here: a directory that does not exist) are swallowed, because a read-only install folder must not stop
        /// the app from starting.
        /// </summary>
        [Fact]
        public void IgnoresIoErrors()
        {
            using var temp = new TempDirectory();

            var exception = Record.Exception(() => CoreLicensor.EnsureFilesExist(temp.Combine("does", "not", "exist")));

            Assert.Null(exception);
        }
    }
}
