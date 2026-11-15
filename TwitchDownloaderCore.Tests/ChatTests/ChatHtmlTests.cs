using System.Net;
using System.Text;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ChatTests
{
    // Tests ChatHtml, which turns a chat into a self-contained HTML page using the embedded chat-template.html. It is the
    // only export that people open directly in a browser, so it must link to the right VOD time, show the right
    // emotes/badges either inline (base64) or by URL, and HTML-encode user text. Third-party emote and badge metadata come
    // from TwitchHelper's (static) HttpClient, which is replaced here with an offline fake.
    [Collection(StaticHttpClientCollection.NAME)]
    public sealed class ChatHtmlTests : IDisposable
    {
        private const string BTTV_ID = "000000000000000000000b01";
        private static readonly byte[] EmoteBytes = [1, 2, 3, 4];
        private static readonly byte[] ThirdPartyBytes = [5, 6, 7];
        private static readonly byte[] BadgeBytes = [8, 9];

        private readonly TempDirectory _cache = new();
        private readonly RecordingTaskProgress _progress = new();
        private readonly FakeHttpMessageHandler _http;
        private readonly HttpClientOverride _httpOverride;

        public ChatHtmlTests()
        {
            _http = new FakeHttpMessageHandler(Respond);
            _httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, _http);
        }

        public void Dispose()
        {
            _httpOverride.Dispose();
            _cache.Dispose();
        }

        // BTTV and FFZ each offer one global emote, 7TV and all channel endpoints are "not found",
        // and GQL serves one global and one channel badge.
        private static HttpResponseMessage Respond(HttpRequestMessage request, string? body)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://api.betterttv.net/3/cached/emotes/global")
                return FakeHttpMessageHandler.Json($$"""[{"id":"{{BTTV_ID}}","code":"TestBttv","imageType":"png","userId":"1"}]""");
            if (url == "https://api.betterttv.net/3/cached/frankerfacez/emotes/global")
                return FakeHttpMessageHandler.Json("""[{"id":100001,"code":"TestFfz","imageType":"png","animated":false}]""");
            if (request.RequestUri.Host == "gql.twitch.tv" && body!.Contains("broadcastBadges"))
                return FakeHttpMessageHandler.Json("""{"data":{"user":{"broadcastBadges":[{"imageURL":"https://example.invalid/sub3.png","title":"3-Month Subscriber","setID":"subscriber","version":"3"}]}}}""");
            if (request.RequestUri.Host == "gql.twitch.tv")
                return FakeHttpMessageHandler.Json("""{"data":{"badges":[{"imageURL":"https://example.invalid/sub0.png","title":"Subscriber","setID":"subscriber","version":"0"},{"imageURL":"https://example.invalid/vip.png","title":"VIP","setID":"vip","version":"1"}]}}""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static Comment Comment(double offset, string displayName, string login, string? color, List<UserBadge>? badges, string body, List<Fragment>? fragments) => new()
        {
            content_offset_seconds = offset,
            commenter = new Commenter { display_name = displayName, name = login },
            message = new Message { body = body, fragments = fragments, user_badges = badges, user_color = color }
        };

        private static ChatRoot CreateChat() => new()
        {
            streamer = new Streamer { id = 100000001, login = "teststreamer", name = "TestStreamer" },
            video = new Video { id = "1000000001" },
            comments =
            [
                Comment(65, "viewer_one", "viewer_one", "#FF0000", [new UserBadge { _id = "subscriber", version = "3" }, new UserBadge { _id = "vip", version = "1" }],
                    "Kappa hello TestBttv TestFfz <script>",
                    [new Fragment { text = "Kappa", emoticon = new Emoticon { emoticon_id = "25" } }, new Fragment { text = " hello TestBttv TestFfz <script>" }]),
                Comment(3725, "テスト", "jp_viewer", null, null, "plain & simple", null),
                Comment(90061, "viewer_two", "viewer_two", null, [new UserBadge { _id = "unknown-badge", version = "1" }, new UserBadge { _id = "subscriber", version = "99" }],
                    "Unknown", [new Fragment { text = "Unknown", emoticon = new Emoticon { emoticon_id = "999" } }]),
            ],
            embeddedData = new EmbeddedData
            {
                firstParty = [new EmbedEmoteData { id = "25", data = EmoteBytes }],
                thirdParty = [new EmbedEmoteData { id = BTTV_ID, name = "TestBttv", data = ThirdPartyBytes }],
                twitchBadges = [new EmbedChatBadge { name = "subscriber", versions = new() { ["3"] = new ChatBadgeData { bytes = BadgeBytes } } }],
            }
        };

        private async Task<string[]> Serialize(ChatRoot chat, bool embedData, string fileName = "chat.html")
        {
            using var ms = new MemoryStream();
            await ChatHtml.SerializeAsync(ms, Path.Combine(_cache.Path, fileName), chat, _cache.Path, _progress, embedData, TestContext.Current.CancellationToken);
            return Encoding.UTF8.GetString(ms.ToArray()).Split(Environment.NewLine);
        }

        private static string CommentLine(string[] lines, string author) => Assert.Single(lines, l => l.StartsWith("<pre class=\"comment-root\">") && l.Contains($">{author}</span>"));

        /// <summary>
        /// The template placeholders (title, custom CSS, custom HTML) must all be replaced and the rest of the template copied
        /// through, otherwise the page renders the raw markers or loses its styling. The title is the HTML-encoded file name.
        /// </summary>
        [Fact]
        public async Task Serialize_ReplacesTemplatePlaceholders()
        {
            var lines = await Serialize(CreateChat(), true, "my <chat>.html");

            Assert.Contains("<!DOCTYPE html>", lines);
            Assert.Contains("my &lt;chat&gt;", lines);
            Assert.DoesNotContain(lines, l => l.Contains("<!-- TITLE -->") || l.Contains("/* [CUSTOM CSS] */") || l.Contains("<!-- CUSTOM HTML -->"));
            Assert.Equal(3, lines.Count(l => l.StartsWith("<pre class=\"comment-root\">")));
            Assert.Equal("</html>", lines.Last(l => l.Length > 0));
        }

        /// <summary>
        /// With embedData the page must work offline: every embedded first-party emote, third-party emote and badge version is
        /// written as a CSS class carrying its image as a base64 data URL.
        /// </summary>
        [Fact]
        public async Task Serialize_Embedded_WritesBase64CssForEveryImage()
        {
            var lines = await Serialize(CreateChat(), true);

            Assert.Contains($".first-25 {{ content:url(\"data:image/png;base64, {Convert.ToBase64String(EmoteBytes)}\"); }}", lines);
            Assert.Contains($".third-{BTTV_ID} {{ content:url(\"data:image/png;base64, {Convert.ToBase64String(ThirdPartyBytes)}\"); }}", lines);
            Assert.Contains($".badge-subscriber-3 {{ content:url(\"data:image/png;base64, {Convert.ToBase64String(BadgeBytes)}\"); }}", lines);
        }

        /// <summary>
        /// Pins the full markup of an embedded comment: VOD link with "?t=0h01m05s" timecode, H:mm:ss label, badge classes,
        /// coloured author linking to the channel, first/third-party emotes as CSS-class images, a third-party emote that has
        /// metadata but no embedded image (TestFfz) left as text, and HTML-encoded message text.
        /// </summary>
        [Fact]
        public async Task Serialize_Embedded_WritesExpectedCommentMarkup()
        {
            var lines = await Serialize(CreateChat(), true);

            Assert.Equal(
                "<pre class=\"comment-root\">[<a href=\"https://twitch.tv/videos/1000000001/?t=0h01m05s\">0:01:05</a>] "
                + "<img class=\"badge-image badge-subscriber-3\" title=\"subscriber\"><span class=\"text-hide\">subscriber</span> "
                + "<img class=\"badge-image badge-vip-1\" title=\"vip\"><span class=\"text-hide\">vip</span> "
                + "<a href=\"https://twitch.tv/viewer_one\"><span class=\"comment-author\" style=\"color: #FF0000\">viewer_one</span></a>"
                + "<span class=\"comment-message\">: "
                + "<img class=\"emote-image first-25\" title=\"Kappa\"><span class=\"text-hide\">Kappa</span> "
                + "hello "
                + $"<img class=\"emote-image third-{BTTV_ID}\" title=\"TestBttv\"><span class=\"text-hide\">TestBttv</span> "
                + "TestFfz &lt;script&gt; </span></pre>",
                CommentLine(lines, "viewer_one"));
        }

        /// <summary>
        /// Comments without fragments (old chats) fall back to the body, null badge lists and colours are tolerated, hours past
        /// 24 keep counting in both the label and the "?t=" timecode, and non-ASCII display names get the ASCII login appended
        /// so the reader can still find the account.
        /// </summary>
        [Fact]
        public async Task Serialize_HandlesLegacyAndInternationalComments()
        {
            var lines = await Serialize(CreateChat(), true);

            Assert.Equal(
                "<pre class=\"comment-root\">[<a href=\"https://twitch.tv/videos/1000000001/?t=1h02m05s\">1:02:05</a>] "
                + "<a href=\"https://twitch.tv/jp_viewer\"><span class=\"comment-author\" >テスト (jp_viewer)</span></a>"
                + "<span class=\"comment-message\">: plain &amp; simple </span></pre>",
                CommentLine(lines, "テスト (jp_viewer)"));
            Assert.StartsWith("<pre class=\"comment-root\">[<a href=\"https://twitch.tv/videos/1000000001/?t=25h01m01s\">25:01:01</a>]", CommentLine(lines, "viewer_two"));
        }

        /// <summary>
        /// A first-party emote that is not embedded must still render, by linking to Twitch's static CDN.
        /// </summary>
        [Fact]
        public async Task Serialize_Embedded_MissingFirstPartyEmote_FallsBackToCdnUrl()
        {
            var lines = await Serialize(CreateChat(), true);

            Assert.Contains(
                "<img class=\"emote-image\" src=\"https://static-cdn.jtvnw.net/emoticons/v2/999/default/dark/1.0\" title=\"Unknown\"><span class=\"text-hide\">Unknown</span> ",
                CommentLine(lines, "viewer_two"));
        }

        /// <summary>
        /// Without embedData no base64 CSS is written; emotes and badges link to their CDN URLs instead. Badge URLs come from
        /// Twitch's badge GQL (channel badges override global ones), and badges/versions Twitch doesn't know are skipped
        /// rather than producing broken images.
        /// </summary>
        [Fact]
        public async Task Serialize_NotEmbedded_LinksToRemoteImages()
        {
            var lines = await Serialize(CreateChat(), false);

            Assert.DoesNotContain(lines, l => l.Contains("base64"));
            Assert.Equal(
                "<pre class=\"comment-root\">[<a href=\"https://twitch.tv/videos/1000000001/?t=0h01m05s\">0:01:05</a>] "
                + "<img class=\"badge-image\" title=\"subscriber\" src=\"https://example.invalid/sub3.png\"><span class=\"text-hide\">subscriber</span> "
                + "<img class=\"badge-image\" title=\"vip\" src=\"https://example.invalid/vip.png\"><span class=\"text-hide\">vip</span> "
                + "<a href=\"https://twitch.tv/viewer_one\"><span class=\"comment-author\" style=\"color: #FF0000\">viewer_one</span></a>"
                + "<span class=\"comment-message\">: "
                + "<img class=\"emote-image\" src=\"https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/1.0\" title=\"Kappa\"><span class=\"text-hide\">Kappa</span> "
                + "hello "
                + $"<img class=\"emote-image\" title=\"TestBttv\" src=\"https://cdn.betterttv.net/emote/{BTTV_ID}/1x\"><span class=\"text-hide\">TestBttv</span> "
                + "<img class=\"emote-image\" title=\"TestFfz\" src=\"https://cdn.betterttv.net/frankerfacez_emote/100001/1\"><span class=\"text-hide\">TestFfz</span> "
                + "&lt;script&gt; </span></pre>",
                CommentLine(lines, "viewer_one"));
            Assert.DoesNotContain("badge-image", CommentLine(lines, "viewer_two"));
        }

        /// <summary>
        /// A failing third-party provider (7TV returns 404 here) must only be logged; the HTML export still completes.
        /// </summary>
        [Fact]
        public async Task Serialize_ProviderFailure_IsLoggedNotThrown()
        {
            var lines = await Serialize(CreateChat(), true);

            Assert.Contains(_progress.Errors, e => e.Contains("7TV"));
            Assert.Equal(3, lines.Count(l => l.StartsWith("<pre class=\"comment-root\">")));
        }

        /// <summary>The export must stop when cancelled instead of writing a partial page.</summary>
        [Fact]
        public async Task Serialize_Cancelled_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            using var ms = new MemoryStream();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ChatHtml.SerializeAsync(ms, "chat.html", CreateChat(), _cache.Path, _progress, true, cts.Token));
            Assert.Equal(0, ms.Length);
        }
    }
}
