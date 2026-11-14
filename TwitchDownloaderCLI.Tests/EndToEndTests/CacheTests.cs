using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of `cache --clear` / `--force-clear`. The CLI deletes "<system temp>/TwitchDownloader"; Cli.Run points the child's
    // system temp folder (TMPDIR/TMP/TEMP) at a private "systemp" directory, so these tests never touch the real cache. The key regression
    // guarded here is deleting more than the cache folder (e.g. the whole temp directory) or deleting without consent.
    public class CacheTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private readonly string _cacheDir;
        private readonly string _neighbour;

        public CacheTests()
        {
            _cacheDir = _temp.Combine("systemp", "TwitchDownloader");
            Directory.CreateDirectory(Path.Combine(_cacheDir, "emotes"));
            File.WriteAllText(Path.Combine(_cacheDir, "emotes", "cached.png"), "png");
            _neighbour = _temp.Combine("systemp", "other-app.tmp");
            File.WriteAllText(_neighbour, "not ours");
        }

        public void Dispose() => _temp.Dispose();

        // --force-clear must delete the TwitchDownloader cache folder without asking, and nothing else in the temp folder.
        [Fact]
        public void ForceClearDeletesOnlyTheCacheFolder()
        {
            var result = Cli.RunOk(_temp.Path, "cache", "--force-clear");

            Assert.Contains("Cache cleared successfully.", result.StdOut);
            Assert.False(Directory.Exists(_cacheDir));
            Assert.True(File.Exists(_neighbour));
        }

        // Clearing when there is no cache must be a successful no-op with a message, not an error.
        [Fact]
        public void ForceClearWithoutCacheIsANoOp()
        {
            Directory.Delete(_cacheDir, true);

            var result = Cli.RunOk(_temp.Path, "cache", "--force-clear");

            Assert.Contains("No cache to clear.", result.StdOut);
            Assert.True(File.Exists(_neighbour));
        }

        // --clear must ask first: "y" deletes, "n" keeps, and no answer (closed stdin) keeps the cache and reports the problem.
        [Theory]
        [InlineData("y\n", false)]
        [InlineData("YES\n", false)]
        [InlineData("n\n", true)]
        [InlineData(null, true)]
        public void ClearAsksForConfirmation(string? answer, bool cacheKept)
        {
            var result = Cli.Run(_temp.Path, ["cache", "--clear"], answer);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Contains("Are you sure you want to clear the cache?", result.StdOut);
            Assert.Equal(cacheKept, Directory.Exists(_cacheDir));
            Assert.True(File.Exists(_neighbour));
            if (answer is null)
            {
                Assert.Contains("Could not read user input.", result.StdOut);
            }
        }
    }
}
