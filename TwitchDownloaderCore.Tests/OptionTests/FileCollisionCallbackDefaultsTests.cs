using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tests.OptionTests
{
    // Tests the default FileCollisionCallback of every download/update/merge options class (Chat/ChatUpdate/Video/Clip/TsMerge).
    // These classes are otherwise plain property bags, so they share one test class instead of five near-empty ones.
    public class FileCollisionCallbackDefaultsTests
    {
        public static TheoryData<string, Func<FileInfo, FileInfo>> DefaultCallbacks => new()
        {
            { nameof(ChatDownloadOptions), new ChatDownloadOptions().FileCollisionCallback },
            { nameof(ChatUpdateOptions), new ChatUpdateOptions().FileCollisionCallback },
            { nameof(VideoDownloadOptions), new VideoDownloadOptions().FileCollisionCallback },
            { nameof(ClipDownloadOptions), new ClipDownloadOptions().FileCollisionCallback },
            { nameof(TsMergeOptions), new TsMergeOptions().FileCollisionCallback },
        };

        /// <summary>
        /// Library callers that never set a callback must get "overwrite the requested file" (identity), not null.
        /// A null default would crash every download that hits an existing file.
        /// </summary>
        [Theory]
        [MemberData(nameof(DefaultCallbacks))]
        public void DefaultCallbackReturnsSameFile(string optionsType, Func<FileInfo, FileInfo> callback)
        {
            var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"{optionsType}.out"));

            Assert.Same(file, callback(file));
        }
    }
}
