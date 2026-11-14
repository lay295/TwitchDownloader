using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tests.OptionTests
{
    // Tests ChatDownloadOptions.FileExtension, which decides the extension of every downloaded chat file.
    public class ChatDownloadOptionsTests
    {
        /// <summary>
        /// The extension combines the format (.json/.html/.txt) with the compression suffix (.gz), so gzipped JSON becomes ".json.gz"
        /// and can be recognized again by the chat updater/renderer. Unknown enum values degrade to no extension instead of throwing.
        /// </summary>
        [Theory]
        [InlineData(ChatFormat.Json, ChatCompression.None, ".json")]
        [InlineData(ChatFormat.Json, ChatCompression.Gzip, ".json.gz")]
        [InlineData(ChatFormat.Html, ChatCompression.None, ".html")]
        [InlineData(ChatFormat.Html, ChatCompression.Gzip, ".html.gz")]
        [InlineData(ChatFormat.Text, ChatCompression.None, ".txt")]
        [InlineData(ChatFormat.Text, ChatCompression.Gzip, ".txt.gz")]
        [InlineData((ChatFormat)99, (ChatCompression)99, "")]
        public void FileExtension_CombinesFormatAndCompression(ChatFormat format, ChatCompression compression, string expected)
        {
            var options = new ChatDownloadOptions { DownloadFormat = format, Compression = compression };

            Assert.Equal(expected, options.FileExtension);
        }

        /// <summary>Defaults are uncompressed JSON with one download thread, matching the CLI's documented defaults.</summary>
        [Fact]
        public void Defaults_AreUncompressedJsonWithOneThread()
        {
            var options = new ChatDownloadOptions();

            Assert.Equal(".json", options.FileExtension);
            Assert.Equal(1, options.DownloadThreads);
        }
    }
}
