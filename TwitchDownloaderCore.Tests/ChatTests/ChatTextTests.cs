using System.Globalization;
using System.Text;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ChatTests
{
    // Tests ChatText, the writer for plain-text (.txt) chat exports. Its four timestamp styles are user-selectable in the
    // GUI and CLI, and people grep/parse these files, so the exact line format must not drift.
    public class ChatTextTests
    {
        private static Comment Comment(DateTime createdAt, double offsetSeconds, string displayName, string body) => new()
        {
            created_at = createdAt,
            content_offset_seconds = offsetSeconds,
            commenter = new Commenter { display_name = displayName, name = displayName.ToLowerInvariant() },
            message = new Message { body = body }
        };

        private static ChatRoot Chat(params Comment[] comments) => new() { comments = [..comments] };

        private static async Task<string[]> Serialize(ChatRoot chat, TimestampFormat format, string culture = "")
        {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            try
            {
                using var ms = new MemoryStream();
                await ChatText.SerializeAsync(ms, chat, format);
                return Encoding.UTF8.GetString(ms.ToArray()).Split(Environment.NewLine);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static readonly ChatRoot SampleChat = Chat(
            Comment(new DateTime(2024, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc), 5.9, "viewer_one", "hello"),
            Comment(new DateTime(2024, 1, 2, 4, 5, 6, 7, DateTimeKind.Utc), 3725.5, "TestStreamer", "one hour in"));

        /// <summary>
        /// UTC format: "[yyyy-MM-dd HH:mm:ss UTC] name: message", one line per comment, milliseconds dropped.
        /// Guards the default export format users parse with scripts.
        /// </summary>
        [Fact]
        public async Task Utc_WritesSecondPrecisionUtcTimestamps()
        {
            var lines = await Serialize(SampleChat, TimestampFormat.Utc);

            Assert.Equal(["[2024-01-02 03:04:05 UTC] viewer_one: hello", "[2024-01-02 04:05:06 UTC] TestStreamer: one hour in", ""], lines);
        }

        /// <summary>UtcFull format adds zero-padded milliseconds; without them, ordering of bursts within one second is lost.</summary>
        [Fact]
        public async Task UtcFull_WritesMillisecondPrecisionUtcTimestamps()
        {
            var lines = await Serialize(SampleChat, TimestampFormat.UtcFull);

            Assert.Equal(["[2024-01-02 03:04:05.678 UTC] viewer_one: hello", "[2024-01-02 04:05:06.007 UTC] TestStreamer: one hour in", ""], lines);
        }

        /// <summary>
        /// Relative format shows the VOD offset as h:mm:ss (fraction truncated, hours not zero-padded) so it can be typed into
        /// the player. Offsets past 24h must keep counting hours ("25:01:01") instead of wrapping into days.
        /// </summary>
        [Theory]
        [InlineData(0, "[0:00:00] viewer: msg")]
        [InlineData(5.9, "[0:00:05] viewer: msg")]
        [InlineData(3725.5, "[1:02:05] viewer: msg")]
        [InlineData(86399, "[23:59:59] viewer: msg")]
        [InlineData(90061, "[25:01:01] viewer: msg")]
        public async Task Relative_WritesVodOffset(double offsetSeconds, string expectedLine)
        {
            var lines = await Serialize(Chat(Comment(default, offsetSeconds, "viewer", "msg")), TimestampFormat.Relative);

            Assert.Equal(expectedLine, lines[0]);
        }

        /// <summary>None format writes just "name: message" with no timestamp prefix.</summary>
        [Fact]
        public async Task None_WritesNoTimestamp()
        {
            var lines = await Serialize(SampleChat, TimestampFormat.None);

            Assert.Equal(["viewer_one: hello", "TestStreamer: one hour in", ""], lines);
        }

        /// <summary>
        /// Output is UTF-8 without a BOM and non-ASCII names/messages (CJK, emoji) pass through untouched, so exports of
        /// international chats stay readable.
        /// </summary>
        [Fact]
        public async Task Output_IsUtf8WithoutBomAndKeepsNonAscii()
        {
            using var ms = new MemoryStream();
            await ChatText.SerializeAsync(ms, Chat(Comment(default, 0, "テスト", "こんにちは 😀")), TimestampFormat.None);

            var bytes = ms.ToArray();
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.Equal("テスト: こんにちは 😀" + Environment.NewLine, Encoding.UTF8.GetString(bytes));
        }

        /// <summary>
        /// The caller owns the output stream (ChatDownloader disposes its FileStream itself), so the writer must flush and
        /// leave it open rather than disposing it.
        /// </summary>
        [Fact]
        public async Task Serialize_LeavesStreamOpen()
        {
            using var ms = new MemoryStream();
            await ChatText.SerializeAsync(ms, SampleChat, TimestampFormat.Utc);

            Assert.True(ms.CanWrite);
            Assert.True(ms.Length > 0);
        }

        /// <summary>An empty chat produces an empty file instead of throwing.</summary>
        [Fact]
        public async Task Serialize_EmptyChat_WritesNothing()
        {
            using var ms = new MemoryStream();
            await ChatText.SerializeAsync(ms, Chat(), TimestampFormat.Utc);

            Assert.Equal(0, ms.Length);
        }

        /// <summary>
        /// The UTC formats use quoted literal separators, so they must be identical under any Gregorian-calendar UI culture
        /// (e.g. de-DE uses '.' date separators, fr-FR differs again).
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        [InlineData("ja-JP")]
        public async Task Utc_IsIndependentOfGregorianCulture(string culture)
        {
            var lines = await Serialize(SampleChat, TimestampFormat.UtcFull, culture);

            Assert.Equal("[2024-01-02 03:04:05.678 UTC] viewer_one: hello", lines[0]);
        }

        /// <summary>
        /// A timestamp labelled "UTC" must always be a Gregorian date. The writer formats with the current culture, so on a
        /// machine set to Thai (Buddhist calendar) the year comes out as 2567.
        /// </summary>
        [Fact(Skip = "Bug: ChatText formats UTC timestamps with CultureInfo.CurrentCulture, so non-Gregorian cultures (e.g. th-TH) write the wrong year")]
        public async Task Utc_IsIndependentOfNonGregorianCulture()
        {
            var lines = await Serialize(SampleChat, TimestampFormat.Utc, "th-TH");

            Assert.Equal("[2024-01-02 03:04:05 UTC] viewer_one: hello", lines[0]);
        }
    }
}
