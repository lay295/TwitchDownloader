using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ToolTests
{
    // Tests PathUtils, which the CLI uses to locate ffmpeg on the PATH before rendering/downloading. A wrong answer either aborts the
    // program although ffmpeg is installed, or lets it run on and fail later with a confusing process-start error.
    // In the process-state collection because the tests rewrite the PATH environment variable.
    [Collection(ProcessStateCollection.NAME)]
    public class PathUtilsTests
    {
        // A path that already points at an existing file is returned as a full path without consulting PATH at all.
        [Fact]
        public void ExistingFilePath_ReturnsFullPath()
        {
            using var temp = new TempDirectory();
            var file = temp.Combine("tool");
            File.WriteAllText(file, "");

            Assert.Equal(Path.GetFullPath(file), PathUtils.GetFileOnPATH(file));
            Assert.True(PathUtils.ExistsOnPATH(file));
        }

        // A bare file name is searched for in every PATH directory (split with the platform's separator), and the first hit wins.
        // Guards the cross-platform PATH separator handling (';' on Windows, ':' elsewhere).
        [Fact]
        public void FileInPathDirectory_IsFound()
        {
            using var temp = new TempDirectory();
            var emptyDir = Directory.CreateDirectory(temp.Combine("empty")).FullName;
            var toolDir = Directory.CreateDirectory(temp.Combine("bin")).FullName;
            const string FILE_NAME = "td-pathutils-test-tool";
            File.WriteAllText(Path.Combine(toolDir, FILE_NAME), "");

            using (new EnvironmentVariableOverride(string.Join(Path.PathSeparator, emptyDir, toolDir), "PATH"))
            {
                Assert.Equal(Path.Combine(toolDir, FILE_NAME), PathUtils.GetFileOnPATH(FILE_NAME));
                Assert.True(PathUtils.ExistsOnPATH(FILE_NAME));
            }
        }

        // A file that is neither at the given path nor in any PATH directory yields null/false instead of throwing.
        [Fact]
        public void MissingFile_ReturnsNull()
        {
            using var temp = new TempDirectory();

            using (new EnvironmentVariableOverride(temp.Path, "PATH"))
            {
                Assert.Null(PathUtils.GetFileOnPATH("td-pathutils-missing-tool"));
                Assert.False(PathUtils.ExistsOnPATH("td-pathutils-missing-tool"));
            }
        }
    }
}
