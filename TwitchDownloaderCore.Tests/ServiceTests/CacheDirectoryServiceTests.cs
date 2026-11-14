using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.ServiceTests
{
    // Tests CacheDirectoryService, which resolves the "TwitchDownloader" temp/cache folder used by every downloader and the
    // GUI/CLI "clear cache" commands. Getting this wrong either scatters cache files or deletes the wrong directory.
    public class CacheDirectoryServiceTests
    {
        private const string SUFFIX = "TwitchDownloader";

        /// <summary>Without a configured folder the cache lives in the system temp directory.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GetCacheDirectory_DefaultsToSystemTemp(string? baseDirectory)
        {
            var expected = Path.Combine(Path.GetFullPath(Path.GetTempPath()), SUFFIX);

            Assert.Equal(expected, CacheDirectoryService.GetCacheDirectory(baseDirectory));
        }

        /// <summary>A user folder gets a "TwitchDownloader" subfolder, so clearing the cache never deletes the user's own folder.</summary>
        [Fact]
        public void GetCacheDirectory_AppendsSuffixToUserFolder()
        {
            using var temp = new TempDirectory();

            Assert.Equal(temp.Combine(SUFFIX), CacheDirectoryService.GetCacheDirectory(temp.Path));
        }

        /// <summary>
        /// A folder already named "TwitchDownloader" (with or without a trailing separator) is used as-is, so the cache is not nested
        /// as TwitchDownloader/TwitchDownloader when the GUI saves the resolved path back to settings.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetCacheDirectory_DoesNotNestSuffix(bool trailingSeparator)
        {
            using var temp = new TempDirectory();
            var cacheDir = temp.Combine(SUFFIX);
            var input = trailingSeparator ? cacheDir + Path.DirectorySeparatorChar : cacheDir;

            var actual = CacheDirectoryService.GetCacheDirectory(input);

            Assert.Equal(cacheDir, Path.TrimEndingDirectorySeparator(actual));
        }

        /// <summary>Relative folders are resolved against the working directory so the cache location does not depend on later cwd changes.</summary>
        [Fact]
        public void GetCacheDirectory_ResolvesRelativePaths()
        {
            var actual = CacheDirectoryService.GetCacheDirectory("relative");

            Assert.True(Path.IsPathFullyQualified(actual));
            Assert.Equal(Path.Combine(Path.GetFullPath("relative"), SUFFIX), actual);
        }

        /// <summary>Clearing deletes the cache folder and its contents, but leaves the parent folder chosen by the user alone.</summary>
        [Fact]
        public void ClearCacheDirectory_DeletesCacheFolderOnly()
        {
            using var temp = new TempDirectory();
            var cacheDir = Directory.CreateDirectory(temp.Combine(SUFFIX, "emotes")).Parent!.FullName;
            File.WriteAllText(Path.Combine(cacheDir, "emotes", "a.png"), "x");
            File.WriteAllText(temp.Combine("keep.txt"), "x");

            var success = CacheDirectoryService.ClearCacheDirectory(temp.Path, out var exception);

            Assert.True(success);
            Assert.Null(exception);
            Assert.False(Directory.Exists(cacheDir));
            Assert.True(File.Exists(temp.Combine("keep.txt")));
        }

        /// <summary>A missing cache folder counts as already cleared (success, no exception) rather than an error.</summary>
        [Fact]
        public void ClearCacheDirectory_SucceedsWhenMissing()
        {
            using var temp = new TempDirectory();

            var success = CacheDirectoryService.ClearCacheDirectory(temp.Path, out var exception);

            Assert.True(success);
            Assert.Null(exception);
        }
    }
}
