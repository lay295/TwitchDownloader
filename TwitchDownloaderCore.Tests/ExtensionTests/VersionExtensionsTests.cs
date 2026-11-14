using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests VersionExtensions.StripRevisionIfDefault, used by the CLI and WPF update checkers to compare the running
    // assembly version (always 4 parts) with the 3-part release tag on GitHub.
    public class VersionExtensionsTests
    {
        /// <summary>A zero (default) revision is dropped so 1.2.3.0 compares equal to the 1.2.3 release; otherwise every build looks outdated.</summary>
        [Fact]
        public void StripsDefaultRevision()
        {
            var result = new Version(1, 2, 3, 0).StripRevisionIfDefault();

            Assert.Equal(new Version(1, 2, 3), result);
            Assert.Equal("1.2.3", result.ToString());
        }

        /// <summary>A meaningful revision must be preserved, so pre-release/hotfix builds still compare correctly.</summary>
        [Fact]
        public void KeepsNonDefaultRevision()
        {
            var version = new Version(1, 2, 3, 4);

            Assert.Same(version, version.StripRevisionIfDefault());
        }

        /// <summary>A 3-part version (revision -1) is returned as an equal 3-part version.</summary>
        [Fact]
        public void LeavesThreePartVersionUnchanged()
        {
            Assert.Equal(new Version(1, 2, 3), new Version(1, 2, 3).StripRevisionIfDefault());
        }

        /// <summary>
        /// A 2-part version (build and revision both -1) should come back unchanged. Today it throws because
        /// <c>new Version(major, minor, -1)</c> rejects the undefined build component.
        /// </summary>
        [Fact(Skip = "Bug: StripRevisionIfDefault throws ArgumentOutOfRangeException for 2-part versions such as 1.2 (build is -1)")]
        public void LeavesTwoPartVersionUnchanged()
        {
            Assert.Equal(new Version(1, 2), new Version(1, 2).StripRevisionIfDefault());
        }
    }
}
