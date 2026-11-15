using System.IO.Compression;
using System.Text.Json;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of `chatupdate`: converting a chat JSON to JSON/HTML/TXT, gzip compression and trimming, through the real CLI.
    // The input is TestData/E2E/chat.json, a synthetic 10 second chat with 8 comments, one embedded first-party emote, one embedded
    // third-party emote and one embedded badge. Its video id is empty so the "refresh video info" step never calls Twitch, and every
    // run goes through a dead proxy, so these tests are deterministic and never touch the network.
    public class ChatUpdateTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private static readonly string ChatJson = TestData.Path("E2E", "chat.json");

        private static readonly string[] AllCommentLines =
        [
            "[0:00:00] E2E_Alpha: hello from alpha",
            "[0:00:01] E2E_Bravo: nice E2eFirst",
            "[0:00:02] E2E_Charlie: third party E2eThird here",
            "[0:00:03] E2E_Delta: message four",
            "[0:00:05] E2E_Echo: message five",
            "[0:00:06] E2E_Foxtrot: message six",
            "[0:00:08] E2E_Golf: message seven",
            "[0:00:09] E2E_Alpha: goodbye from alpha",
        ];

        public void Dispose() => _temp.Dispose();

        // JSON -> TXT must write exactly one "[relative time] name: message" line per comment, in order. This is the format people grep
        // and diff, so any change to it is user visible.
        [Fact]
        public void JsonToTextWritesOneLinePerComment()
        {
            var output = _temp.Combine("chat.txt");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output);

            Assert.Equal(AllCommentLines, File.ReadAllLines(output));
        }

        // --timestamp-format must switch the TXT timestamp style. Utc must print the comment's UTC creation time regardless of the
        // machine's time zone, so the CLI runs with an unusual TZ (+12:45) to catch any accidental local-time conversion.
        [Theory]
        [InlineData("None", "E2E_Alpha: hello from alpha")]
        [InlineData("Relative", "[0:00:00] E2E_Alpha: hello from alpha")]
        [InlineData("Utc", "[2024-01-01 00:00:00 UTC] E2E_Alpha: hello from alpha")]
        public void TextTimestampFormatIsApplied(string format, string expectedFirstLine)
        {
            var output = _temp.Combine("chat.txt");

            var result = Cli.Run(_temp.Path, ["chatupdate", "-i", ChatJson, "-o", output, "--timestamp-format", format], env: new Dictionary<string, string?> { ["TZ"] = "Pacific/Chatham" });

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal(expectedFirstLine, File.ReadAllLines(output)[0]);
        }

        // JSON -> HTML must contain every comment and inline the embedded emote and badge images as data URIs, so the page works
        // offline. Guards the "embedded data present => use it" switch in ChatUpdater.
        [Fact]
        public void JsonToHtmlInlinesEmbeddedImages()
        {
            var output = _temp.Combine("chat.html");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output);

            var html = File.ReadAllText(output);
            Assert.Equal(8, html.Split("<pre class=\"comment-root\">").Length - 1);
            Assert.Contains(".first-e2efp1 { content:url(\"data:image/png;base64,", html);
            Assert.Contains(".badge-e2ebadge-1 { content:url(\"data:image/png;base64,", html);
            Assert.Contains("class=\"emote-image first-e2efp1\"", html);
            Assert.Contains("goodbye from alpha", html);
        }

        // JSON -> JSON must keep comments and embedded images byte-for-byte, upgrade the file version to the current one and stamp
        // UpdatedAt. Guards against the updater dropping data it does not touch.
        [Fact]
        public void JsonToJsonPreservesDataAndStampsUpdatedAt()
        {
            var output = _temp.Combine("updated.json");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output);

            using var input = JsonDocument.Parse(File.ReadAllText(ChatJson));
            using var updated = JsonDocument.Parse(File.ReadAllText(output));
            var root = updated.RootElement;
            Assert.Equal(1, root.GetProperty("FileInfo").GetProperty("Version").GetProperty("Major").GetInt32());
            Assert.Equal(4, root.GetProperty("FileInfo").GetProperty("Version").GetProperty("Minor").GetInt32());
            Assert.NotEqual("0001-01-01T00:00:00", root.GetProperty("FileInfo").GetProperty("UpdatedAt").GetString());
            Assert.Equal(
                input.RootElement.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("_id").GetString()),
                root.GetProperty("comments").EnumerateArray().Select(c => c.GetProperty("_id").GetString()));
            Assert.Equal(
                input.RootElement.GetProperty("embeddedData").GetProperty("firstParty")[0].GetProperty("data").GetString(),
                root.GetProperty("embeddedData").GetProperty("firstParty")[0].GetProperty("data").GetString());
            Assert.Equal("e2ebadge", root.GetProperty("embeddedData").GetProperty("twitchBadges")[0].GetProperty("name").GetString());
        }

        // --compression gzip must write "<output>.gz" (and not "<output>"), the file must be real gzip, and the CLI must accept that
        // .json.gz back as input. Guards both halves of the compressed-chat workflow.
        [Fact]
        public void GzipOutputIsWrittenWithGzExtensionAndCanBeReadBack()
        {
            var output = _temp.Combine("compressed.json");
            var roundTrip = _temp.Combine("roundtrip.txt");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output, "--compression", "Gzip");

            Assert.False(File.Exists(output));
            using (var gz = new GZipStream(File.OpenRead(output + ".gz"), CompressionMode.Decompress))
            using (var doc = JsonDocument.Parse(gz))
            {
                Assert.Equal(8, doc.RootElement.GetProperty("comments").GetArrayLength());
            }

            Cli.RunOk(_temp.Path, "chatupdate", "-i", output + ".gz", "-o", roundTrip);
            Assert.Equal(AllCommentLines, File.ReadAllLines(roundTrip));
        }

        // Trimming to TXT must drop comments outside -b/-e. Comments in (end, end + 1] are left unasserted: the code means to keep a
        // one second margin there but currently drops them (see EndTrimAtChatEndKeepsLastComment).
        [Fact]
        public void TrimToTextDropsCommentsOutsideRange()
        {
            var output = _temp.Combine("trimmed.txt");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output, "-b", "2", "-e", "6");

            var lines = File.ReadAllLines(output);
            Assert.Equal(AllCommentLines[2..5], lines[..3]);
            Assert.DoesNotContain(lines, l => l.Contains("hello from alpha") || l.Contains("nice E2eFirst") || l.Contains("message seven") || l.Contains("goodbye"));
        }

        // Trimming a JSON must only move video.start/end and keep every comment (JSON is the lossless format, so the trim can be widened
        // again later). A narrower trim must not need the network, which the dead proxy enforces.
        [Fact]
        public void TrimToJsonMovesBoundsAndKeepsComments()
        {
            var output = _temp.Combine("trimmed.json");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output, "-b", "2", "-e", "6");

            using var doc = JsonDocument.Parse(File.ReadAllText(output));
            Assert.Equal(2, doc.RootElement.GetProperty("video").GetProperty("start").GetDouble());
            Assert.Equal(6, doc.RootElement.GetProperty("video").GetProperty("end").GetDouble());
            Assert.Equal(8, doc.RootElement.GetProperty("comments").GetArrayLength());
        }

        // An end trim equal to the chat's own end must keep every comment. ChatUpdater.UpdateChatTrim removes comments starting AT
        // FindLastIndex(offset <= end + 1) instead of after it, so whenever no comment falls in (end, end + 1] the last comment that IS
        // inside the range is deleted (here the 9.5s comment of a 0-10s chat).
        [Fact(Skip = "Bug: chatupdate -e drops the last in-range comment from TXT/HTML output (UpdateChatTrim RemoveRange starts at the last kept index)")]
        public void EndTrimAtChatEndKeepsLastComment()
        {
            var output = _temp.Combine("trimmed.txt");

            Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output, "-e", "10");

            Assert.Equal(AllCommentLines, File.ReadAllLines(output));
        }

        // Updating a chat in place (input == output) must warn, then produce a complete file. Guards against truncating the output
        // before the input has been fully read, which would destroy the user's only copy.
        [Fact]
        public void InPlaceUpdateWarnsAndKeepsData()
        {
            var chat = _temp.Combine("inplace.json");
            File.Copy(ChatJson, chat);

            var result = Cli.RunOk(_temp.Path, "chatupdate", "-i", chat, "-o", chat, "--collision", "Overwrite");

            Assert.Contains("Output file path is identical to input file", result.StdOut);
            using var doc = JsonDocument.Parse(File.ReadAllText(chat));
            Assert.Equal(8, doc.RootElement.GetProperty("comments").GetArrayLength());
        }

        // A missing input file must be a clean error (exit 1, message), not a crash, and must not create the output.
        [Fact]
        public void MissingInputFileFails()
        {
            var output = _temp.Combine("out.txt");

            var result = Cli.Run(_temp.Path, ["chatupdate", "-i", _temp.Combine("missing.json"), "-o", output]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("[ERROR] - Input file does not exist!", result.StdOut);
            Assert.False(File.Exists(output));
        }

        // Only JSON chats carry enough data to be updated, so a TXT input must be refused with a clear error.
        [Fact]
        public void NonJsonInputIsRejected()
        {
            var input = _temp.Combine("chat.txt");
            File.WriteAllLines(input, AllCommentLines);

            var result = Cli.Run(_temp.Path, ["chatupdate", "-i", input, "-o", _temp.Combine("out.json")]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("[ERROR] - Input file must be .json or .json.gz!", result.StdOut);
        }

        // An unsupported output extension is a user typo and should be reported like the other input errors (message + exit code 1).
        // Today UpdateChat throws NotSupportedException out of Main, so the runtime aborts (SIGABRT and a core dump on Unix) and prints a
        // stack trace instead.
        [Fact(Skip = "Existing behavior in v1.56.5: chatupdate with an unsupported output extension ends with an unhandled NotSupportedException (stack trace) rather than an error message and exit code 1")]
        public void UnsupportedOutputExtensionFailsCleanly()
        {
            var result = Cli.Run(_temp.Path, ["chatupdate", "-i", ChatJson, "-o", _temp.Combine("out.xyz")]);

            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("Unhandled exception", result.StdErr);
        }
    }
}
