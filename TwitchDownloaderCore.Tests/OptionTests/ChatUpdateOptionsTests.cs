using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore.Tests.OptionTests
{
    // Tests ChatUpdateOptions.FileExtension, which decides the extension of chat files rewritten by the chat updater.
    public class ChatUpdateOptionsTests
    {
        /// <summary>
        /// Same rules as the chat downloader: format extension plus ".gz" for gzip, and no extension for unknown enum values.
        /// Kept separate because the two option classes duplicate the logic and could drift apart.
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
            var options = new ChatUpdateOptions { OutputFormat = format, Compression = compression };

            Assert.Equal(expected, options.FileExtension);
        }
    }
}
