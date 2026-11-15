using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests DownloadChat.GetDownloadOptions, which turns parsed `chatdownload` arguments into Core's ChatDownloadOptions. The output
    // extension alone decides the chat format, and gzip compression changes the file name, so mistakes here produce the wrong kind of file.
    // Download() is only tested for the failure path (Twitch faked to 404); the null/unparsable-id branches call Environment.Exit.
    // In the static-HttpClient collection because that test swaps TwitchHelper.httpClient.
    [Collection(StaticHttpClientCollection.NAME)]
    public class DownloadChatTests
    {
        private static ChatDownloadOptions Map(params string[] argv)
        {
            var args = CliArgs.Parse<ChatDownloadArgs>(argv);
            return DownloadChat.GetDownloadOptions(args, new FileCollisionHandler(args, new RecordingTaskProgress()), new RecordingTaskProgress());
        }

        // All options must be copied over, a VOD URL reduced to its id, and trims converted to seconds.
        [Fact]
        public void MapsAllOptions()
        {
            var options = Map(
                "chatdownload", "--id", "https://twitch.tv/videos/987654321", "-o", "chat.json", "-b", "1m", "-e", "1500ms", "-E",
                "--bttv", "false", "--ffz", "true", "--stv", "false", "--timestamp-format", "Utc", "-t", "3", "--temp-path", "cache");

            Assert.Equal("987654321", options.Id);
            Assert.Equal(ChatFormat.Json, options.DownloadFormat);
            Assert.Equal("chat.json", options.Filename);
            Assert.True(options.TrimBeginning);
            Assert.Equal(60, options.TrimBeginningTime);
            Assert.True(options.TrimEnding);
            Assert.Equal(1.5, options.TrimEndingTime);
            Assert.True(options.EmbedData);
            Assert.False(options.BttvEmotes);
            Assert.True(options.FfzEmotes);
            Assert.False(options.StvEmotes);
            Assert.Equal(TimestampFormat.Utc, options.TimeFormat);
            Assert.Equal(3, options.DownloadThreads);
            Assert.Equal("cache", options.TempFolder);
            Assert.Equal(ChatCompression.None, options.Compression);
        }

        // Omitted trims (zero) must not enable trimming, and a clip slug must be accepted as the id.
        [Fact]
        public void DefaultsDoNotTrim()
        {
            var options = Map("chatdownload", "--id", "SomeClipSlug", "-o", "chat.json");

            Assert.Equal("SomeClipSlug", options.Id);
            Assert.False(options.TrimBeginning);
            Assert.False(options.TrimEnding);
            Assert.True(options.BttvEmotes);
        }

        // The output extension (case-insensitive) selects the chat format; no extension means plain text.
        [Theory]
        [InlineData("chat.json", ChatFormat.Json)]
        [InlineData("chat.JSON", ChatFormat.Json)]
        [InlineData("chat.html", ChatFormat.Html)]
        [InlineData("chat.htm", ChatFormat.Html)]
        [InlineData("chat.txt", ChatFormat.Text)]
        [InlineData("chat.text", ChatFormat.Text)]
        [InlineData("chat", ChatFormat.Text)]
        public void ExtensionSelectsFormat(string output, ChatFormat expected)
        {
            Assert.Equal(expected, Map("chatdownload", "--id", "123", "-o", output).DownloadFormat);
        }

        // An unknown extension must be rejected instead of silently writing, say, JSON into a .csv file.
        [Fact]
        public void UnknownExtension_Throws()
        {
            Assert.Throws<NotSupportedException>(() => Map("chatdownload", "--id", "123", "-o", "chat.csv"));
        }

        // Gzip compression appends ".gz" to the requested name so the file is recognisable as compressed.
        [Fact]
        public void GzipCompression_AppendsGzExtension()
        {
            var options = Map("chatdownload", "--id", "123", "-o", "chat.json", "--compression", "Gzip");

            Assert.Equal(ChatCompression.Gzip, options.Compression);
            Assert.Equal("chat.json.gz", options.Filename);
        }

        // The verb entry point hands the parsed VOD id to Core and lets a Twitch API failure propagate, without leaving an output file behind.
        [Fact]
        public void Download_TwitchFailure_PropagatesAndLeavesNoOutput()
        {
            using var temp = new TempDirectory();
            var output = temp.Combine("chat.json");
            var http = FakeHttpMessageHandler.NotFound();
            using var httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, http);

            var ex = Assert.Throws<AggregateException>(() => DownloadChat.Download(CliArgs.Parse<ChatDownloadArgs>(
                "chatdownload", "--id", "123456789", "-o", output, "--collision", "Overwrite", "--log-level", "None")));

            Assert.IsType<HttpRequestException>(ex.InnerException);
            Assert.Contains(http.Requests, r => r.Body!.Contains("video(id:\\\"123456789\\\")"));
            Assert.False(File.Exists(output));
        }
    }
}
