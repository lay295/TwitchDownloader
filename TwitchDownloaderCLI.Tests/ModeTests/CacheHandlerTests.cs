using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests CacheHandler, the `cache` verb that deletes TwitchDownloader's cache folder under the system temp directory.
    // It is destructive, so it must only delete after --force-clear or a "yes" to the prompt.
    // The system temp directory is redirected (TMPDIR/TMP/TEMP) to a throwaway folder so the real cache, which other test processes
    // may be using, is never touched; that is process-wide state, hence the non-parallel collection.
    [Collection(ProcessStateCollection.NAME)]
    public class CacheHandlerTests
    {
        private sealed class FakeTempPath : IDisposable
        {
            private readonly TempDirectory _root = new();
            private readonly EnvironmentVariableOverride _env;

            public string CacheDirectory => Path.Combine(_root.Path, "TwitchDownloader");

            public FakeTempPath()
            {
                _env = new EnvironmentVariableOverride(_root.Path, "TMPDIR", "TMP", "TEMP");

                // Never run a cache-clearing test against the real temp directory.
                if (Path.TrimEndingDirectorySeparator(Path.GetTempPath()) != _root.Path)
                {
                    Dispose();
                    Assert.Skip("Could not redirect Path.GetTempPath() on this platform");
                }
            }

            public void CreateCache()
            {
                Directory.CreateDirectory(Path.Combine(CacheDirectory, "VideoCache"));
                File.WriteAllText(Path.Combine(CacheDirectory, "VideoCache", "part.ts"), "data");
            }

            public void Dispose()
            {
                _env.Dispose();
                _root.Dispose();
            }
        }

        // --force-clear deletes the whole cache folder without asking and reports progress.
        [Fact]
        public void ForceClear_DeletesCache()
        {
            using var tempPath = new FakeTempPath();
            tempPath.CreateCache();
            using var console = new ConsoleCapture();

            CacheHandler.ParseArgs(CliArgs.Parse<CacheArgs>("cache", "--force-clear"));

            Assert.False(Directory.Exists(tempPath.CacheDirectory));
            Assert.Equal("[INFO] - Clearing cache...\n[INFO] - Cache cleared successfully.\n", console.Output);
        }

        // Clearing when there is no cache folder is not an error; the user is told there was nothing to clear.
        [Fact]
        public void ForceClear_WithoutCache_ReportsNothingToClear()
        {
            using var tempPath = new FakeTempPath();
            using var console = new ConsoleCapture();

            CacheHandler.ParseArgs(CliArgs.Parse<CacheArgs>("cache", "--force-clear"));

            Assert.Equal("[INFO] - No cache to clear.\n", console.Output);
        }

        // --clear asks first: answering yes deletes the cache, answering no must leave it intact.
        [Theory]
        [InlineData("y", false)]
        [InlineData("n", true)]
        public void Clear_DeletesOnlyAfterConfirmation(string answer, bool cacheRemains)
        {
            using var tempPath = new FakeTempPath();
            tempPath.CreateCache();
            using var console = new ConsoleCapture(answer + "\n");

            CacheHandler.ParseArgs(CliArgs.Parse<CacheArgs>("cache", "--clear"));

            Assert.Equal(cacheRemains, Directory.Exists(tempPath.CacheDirectory));
            Assert.StartsWith("Are you sure you want to clear the cache?", console.Output);
        }

        // Running `cache` without a flag must not delete anything.
        [Fact]
        public void NoFlags_DoesNothing()
        {
            using var tempPath = new FakeTempPath();
            tempPath.CreateCache();
            using var console = new ConsoleCapture();

            CacheHandler.ParseArgs(CliArgs.Parse<CacheArgs>("cache"));

            Assert.True(Directory.Exists(tempPath.CacheDirectory));
            Assert.Equal("", console.Output);
        }
    }
}
