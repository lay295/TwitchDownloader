using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests UpdateChat.GetUpdateOptions, which validates `chatupdate` input/output paths and maps the arguments to Core's ChatUpdateOptions.
    // Its trim semantics differ from chatdownload (-1 = keep, 0 = trim to start), which is easy to break.
    // The missing-input and non-JSON-input branches call Environment.Exit, so they are not unit tested; Update() is only tested for a corrupt input.
    public class UpdateChatTests
    {
        private static ChatUpdateOptions Map(RecordingTaskProgress logger, params string[] argv)
        {
            var args = CliArgs.Parse<ChatUpdateArgs>(argv);
            return UpdateChat.GetUpdateOptions(args, new FileCollisionHandler(args, logger), logger);
        }

        // All options must be copied over and the output extension must select the output format.
        [Fact]
        public void MapsAllOptions()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("in.json");
            File.WriteAllText(input, "{}");
            var output = temp.Combine("out.html");

            var options = Map(new RecordingTaskProgress(),
                "chatupdate", "-i", input, "-o", output, "-E", "-R", "-b", "30s", "-e", "2m",
                "--bttv", "false", "--ffz", "false", "--stv", "true", "--timestamp-format", "None", "--temp-path", "cache");

            Assert.Equal(input, options.InputFile);
            Assert.Equal(output, options.OutputFile);
            Assert.Equal(ChatFormat.Html, options.OutputFormat);
            Assert.True(options.EmbedMissing);
            Assert.True(options.ReplaceEmbeds);
            Assert.True(options.TrimBeginning);
            Assert.Equal(30, options.TrimBeginningTime);
            Assert.True(options.TrimEnding);
            Assert.Equal(120, options.TrimEndingTime);
            Assert.False(options.BttvEmotes);
            Assert.False(options.FfzEmotes);
            Assert.True(options.StvEmotes);
            Assert.Equal(TimestampFormat.None, options.TextTimestampFormat);
            Assert.Equal("cache", options.TempFolder);
        }

        // The default -1s trims mean "keep the file's current trim" (disabled), while an explicit 0 must enable trimming to the very start,
        // unlike chatdownload where 0 means "not set".
        [Fact]
        public void TrimSemantics_MinusOneKeepsZeroTrims()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("in.json");
            File.WriteAllText(input, "{}");

            var keep = Map(new RecordingTaskProgress(), "chatupdate", "-i", input, "-o", temp.Combine("out.json"));
            var zero = Map(new RecordingTaskProgress(), "chatupdate", "-i", input, "-o", temp.Combine("out.json"), "-b", "0", "-e", "0");

            Assert.False(keep.TrimBeginning);
            Assert.False(keep.TrimEnding);
            Assert.True(zero.TrimBeginning);
            Assert.Equal(0, zero.TrimBeginningTime);
            Assert.True(zero.TrimEnding);
        }

        // Both .json and gzipped .json.gz inputs are accepted, and every supported output extension maps to its format.
        [Theory]
        [InlineData("in.json", "out.json", ChatFormat.Json)]
        [InlineData("in.json.gz", "out.HTM", ChatFormat.Html)]
        [InlineData("in.JSON", "out.txt", ChatFormat.Text)]
        [InlineData("in.json", "out.text", ChatFormat.Text)]
        [InlineData("in.json", "out", ChatFormat.Text)]
        public void SelectsOutputFormat(string inputName, string outputName, ChatFormat expected)
        {
            using var temp = new TempDirectory();
            var input = temp.Combine(inputName);
            File.WriteAllText(input, "{}");

            Assert.Equal(expected, Map(new RecordingTaskProgress(), "chatupdate", "-i", input, "-o", temp.Combine(outputName)).OutputFormat);
        }

        // Unknown input or output extensions must be rejected rather than guessed.
        [Theory]
        [InlineData("in.xml", "out.json")]
        [InlineData("in.json", "out.csv")]
        public void UnknownExtension_Throws(string inputName, string outputName)
        {
            using var temp = new TempDirectory();
            var input = temp.Combine(inputName);
            File.WriteAllText(input, "{}");

            Assert.Throws<NotSupportedException>(() => Map(new RecordingTaskProgress(), "chatupdate", "-i", input, "-o", temp.Combine(outputName)));
        }

        // Updating a file in place is allowed but must warn, because a failure half-way would destroy the only copy.
        [Fact]
        public void SameInputAndOutput_Warns()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("chat.json");
            File.WriteAllText(input, "{}");
            var logger = new RecordingTaskProgress();

            Map(logger, "chatupdate", "-i", input, "-o", input);

            Assert.Contains(logger.Warnings, w => w.Contains("identical to input file"));
        }

        // A different output must not trigger the in-place warning.
        [Fact]
        public void DifferentOutput_DoesNotWarn()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("chat.json");
            File.WriteAllText(input, "{}");
            var logger = new RecordingTaskProgress();

            Map(logger, "chatupdate", "-i", input, "-o", temp.Combine("chat2.json"));

            Assert.Empty(logger.Warnings);
        }

        // Gzip compression appends ".gz" to the output name.
        [Fact]
        public void GzipCompression_AppendsGzExtension()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("in.json");
            File.WriteAllText(input, "{}");

            var options = Map(new RecordingTaskProgress(), "chatupdate", "-i", input, "-o", temp.Combine("out.json"), "--compression", "gzip");

            Assert.Equal(ChatCompression.Gzip, options.Compression);
            Assert.Equal(temp.Combine("out.json.gz"), options.OutputFile);
        }

        // The verb entry point surfaces a corrupt input file as an error before any output file is created.
        [Fact]
        public void Update_CorruptInput_ThrowsWithoutOutput()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("chat.json");
            File.WriteAllText(input, "this is not json");
            var output = temp.Combine("out.json");

            var ex = Assert.Throws<AggregateException>(() => UpdateChat.Update(CliArgs.Parse<ChatUpdateArgs>(
                "chatupdate", "-i", input, "-o", output, "--collision", "Overwrite", "--log-level", "None")));

            Assert.Contains("is not a valid chat format", Assert.IsType<NotSupportedException>(ex.InnerException).Message);
            Assert.False(File.Exists(output));
        }
    }
}
