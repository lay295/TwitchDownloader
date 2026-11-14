using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ChatTests
{
    // Tests ChatJson, the reader/writer for TwitchDownloader's chat .json/.json.gz files. Every render, update and
    // HTML/TXT conversion starts by loading one of these, and users keep years-old files around, so both the
    // file-format sniffing and the in-place upgrade of older schema versions (1.0.0 through 1.4.0) must keep working.
    // UpgradeChatJson may ask Twitch for the streamer's login, so TwitchHelper's HttpClient is replaced with a fake.
    [Collection(StaticHttpClientCollection.NAME)]
    public sealed class ChatJsonTests : IDisposable
    {
        private const string SAMPLE = "sample_chat_small.json";
        private const int SAMPLE_COMMENT_COUNT = 31;

        private static readonly byte[] PngMagic = [0x89, (byte)'P', (byte)'N', (byte)'G'];

        private readonly TempDirectory _temp = new();
        private readonly FakeHttpMessageHandler _http;
        private readonly HttpClientOverride _httpOverride;
        private Func<HttpResponseMessage> _gqlResponse = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        public ChatJsonTests()
        {
            _http = new FakeHttpMessageHandler((_, _) => _gqlResponse());
            _httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, _http);
        }

        public void Dispose()
        {
            _httpOverride.Dispose();
            _temp.Dispose();
        }

        private static string Fixture(string name) => TestData.Path("Chat", name);

        private static Task<ChatRoot> Load(string path, bool getComments = true, bool onlyFirstAndLastComments = false, bool getEmbeds = true)
            => ChatJson.DeserializeAsync(path, getComments, onlyFirstAndLastComments, getEmbeds, TestContext.Current.CancellationToken);

        private string WriteTemp(string json, string name = "chat.json")
        {
            var path = _temp.Combine(name);
            File.WriteAllText(path, json);
            return path;
        }

        private string WriteTemp(byte[] bytes, string name)
        {
            var path = _temp.Combine(name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static async Task<byte[]> Serialize(ChatRoot chatRoot)
        {
            using var ms = new MemoryStream();
            await ChatJson.SerializeAsync(ms, chatRoot, TestContext.Current.CancellationToken);
            return ms.ToArray();
        }

        private void RespondWithUser(string id, string login, string displayName)
        {
            _gqlResponse = () => FakeHttpMessageHandler.Json(
                $$$"""{"data":{"users":[{"id":"{{{id}}}","displayName":"{{{displayName}}}","login":"{{{login}}}"}]},"extensions":{}}""");
        }

        // Builds a minimal comment for the inline legacy-chat tests below.
        private static string Comment(double offset, string channelId, string commenterId, string login, string display, string body,
            int bits = 0, string? badge = null, string createdAt = "2024-01-01T00:00:00Z")
        {
            var badges = badge is null ? "[]" : $$$"""[{"_id":"{{{badge}}}","version":"1"}]""";
            var offsetText = offset.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return $$$"""
                {"_id":"c{{{offsetText}}}","created_at":"{{{createdAt}}}","channel_id":"{{{channelId}}}","content_type":"video","content_id":"1000000001",
                 "content_offset_seconds":{{{offsetText}}},
                 "commenter":{"display_name":"{{{display}}}","_id":"{{{commenterId}}}","name":"{{{login}}}","logo":null},
                 "message":{"body":"{{{body}}}","bits_spent":{{{bits}}},"fragments":[{"text":"{{{body}}}","emoticon":null}],"user_badges":{{{badges}}},"user_color":null,"emoticons":[]}}
                """;
        }

        // Shared assertions for the committed 1.4.0 sample so every encoding/compression variant is checked identically.
        private static void AssertIsSample(ChatRoot chat)
        {
            Assert.Equal(new ChatRootVersion(1, 4, 0), chat.FileInfo.Version);
            Assert.Equal("teststreamer", chat.streamer.login);
            Assert.Equal(100000001, chat.streamer.id);
            Assert.Equal("1000000001", chat.video.id);
            Assert.Equal(SAMPLE_COMMENT_COUNT, chat.comments.Count);
            Assert.Contains(chat.comments, c => c.message.body == "こんにちは \U0001F600"); // non-ASCII survived decoding
            Assert.Equal(4, chat.embeddedData.thirdParty.Count);
            Assert.Equal(3, chat.embeddedData.firstParty.Count);
        }

        /// <summary>
        /// Loads the committed 1.4.0 sample and checks every section (FileInfo, streamer, video incl. chapter "type" mapping,
        /// comments, user_notice_params, embedded emotes/badges/bits). Guards against a renamed or mis-mapped property
        /// silently dropping data from current-format chat files.
        /// </summary>
        [Fact]
        public async Task Deserialize_CurrentVersion_ReadsEverySection()
        {
            var chat = await Load(Fixture(SAMPLE));

            AssertIsSample(chat);
            Assert.Equal(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), chat.FileInfo.CreatedAt.ToUniversalTime());
            Assert.Equal("TestStreamer", chat.streamer.name);
            Assert.Null(chat.clipper);

            Assert.Equal("Sample synthetic stream", chat.video.title);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), chat.video.created_at.ToUniversalTime());
            Assert.Equal(0, chat.video.start);
            Assert.Equal(120, chat.video.end);
            Assert.Equal(120, chat.video.length);
            Assert.Equal(42, chat.video.viewCount);
            var chapter = Assert.Single(chat.video.chapters);
            Assert.Equal("GAME_CHANGE", chapter._type);
            Assert.Equal(120000, chapter.lengthMilliseconds);

            var kappa = chat.comments.Single(c => c.commenter.name == "moduser" && c.message.body.StartsWith("Kappa"));
            Assert.Equal("25", kappa.message.fragments[0].emoticon.emoticon_id);
            Assert.Null(kappa.message.fragments[1].emoticon);
            Assert.Equal("moderator", Assert.Single(kappa.message.user_badges)._id);
            Assert.Equal(0, Assert.Single(kappa.message.emoticons).begin);

            var cheer = chat.comments.First(c => c.message.body.StartsWith("Cheer1000"));
            Assert.Equal(1000, cheer.message.bits_spent);

            var highlighted = chat.comments.Single(c => c.message.user_notice_params is not null);
            Assert.Equal("highlighted-message", highlighted.message.user_notice_params.msg_id);

            Assert.True(chat.embeddedData.thirdParty.Single(e => e.name == "TestZeroWidth").isZeroWidth);
            Assert.False(chat.embeddedData.thirdParty.Single(e => e.name == "TestBttv").isZeroWidth);
            Assert.All(chat.embeddedData.firstParty, e => Assert.Equal(2, e.imageScale));

            var subscriber = chat.embeddedData.twitchBadges.Single(b => b.name == "subscriber");
            Assert.Equal(["0", "3"], subscriber.versions.Keys.Order());
            Assert.Equal("3-Month Subscriber", subscriber.versions["3"].title);
            Assert.Equal(PngMagic, subscriber.versions["3"].bytes[..4]);

            var bits = Assert.Single(chat.embeddedData.twitchBits);
            Assert.Equal("Cheer", bits.prefix);
            Assert.Equal([1, 100, 1000], bits.tierList.Keys.Order());
            Assert.Equal("Cheer100", bits.tierList[100].name);
        }

        /// <summary>
        /// sample_chat_small.json is shared with the renderer/downloader/CLI tests as an offline-renderable chat. Every
        /// first-party emote, third-party emote word, badge version and cheer it references must be embedded and decodable,
        /// otherwise those tests would silently fall back to the network or render placeholders.
        /// </summary>
        [Fact]
        public async Task SampleChat_IsSelfContained()
        {
            var chat = await Load(Fixture(SAMPLE));
            var embedded = chat.embeddedData;

            var emotes = embedded.firstParty.Concat(embedded.thirdParty).Concat(embedded.twitchBits.SelectMany(b => b.tierList.Values))
                .Select(e => new TwitchEmote(e.data, null, EmoteProvider.FirstParty, e.imageScale, e.id, e.name)).ToList();
            var badges = embedded.twitchBadges.Select(b => new ChatBadge(b.name, b.versions)).ToList();
            try
            {
                Assert.Contains(emotes, e => e.FrameCount > 1); // at least one animated emote
                foreach (var comment in chat.comments)
                {
                    foreach (var fragment in comment.message.fragments.Where(f => f.emoticon is not null))
                        Assert.Contains(embedded.firstParty, e => e.id == fragment.emoticon.emoticon_id);
                    foreach (var badge in comment.message.user_badges)
                        Assert.True(embedded.twitchBadges.Single(b => b.name == badge._id).versions.ContainsKey(badge.version), $"{badge._id}/{badge.version} not embedded");
                    if (comment.message.bits_spent > 0)
                        Assert.Contains("Cheer", comment.message.body);
                }

                var words = chat.comments.SelectMany(c => c.message.body.Split(' ')).ToHashSet();
                Assert.All(embedded.thirdParty, e => Assert.Contains(e.name, words));
            }
            finally
            {
                emotes.ForEach(e => e.Dispose());
                badges.ForEach(b => b.Dispose());
            }
        }

        /// <summary>
        /// A complete current-format chat must load without any network request. Offline rendering and the test
        /// suites of the renderer/updater depend on this; a regression here would make them slow, flaky or fail without internet.
        /// </summary>
        [Fact]
        public async Task Deserialize_CurrentVersion_DoesNotTouchNetwork()
        {
            await Load(Fixture(SAMPLE));

            Assert.Empty(_http.Requests);
        }

        /// <summary>
        /// Chat files can be written gzip-compressed (.json.gz). The loader sniffs the gzip magic bytes rather than the
        /// extension, so a compressed file must load to the same content as the plain one.
        /// </summary>
        [Theory]
        [InlineData("chat.json.gz")]
        [InlineData("chat_without_gz_extension.json")]
        public async Task Deserialize_GzipCompressed_IsDetectedByMagicBytes(string fileName)
        {
            using var ms = new MemoryStream();
            await using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            {
                await gz.WriteAsync(await File.ReadAllBytesAsync(Fixture(SAMPLE), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            }

            var chat = await Load(WriteTemp(ms.ToArray(), fileName));

            AssertIsSample(chat);
        }

        /// <summary>
        /// Files re-saved by text editors (notably on Windows) can gain a UTF-8 BOM or be converted to UTF-16/UTF-32.
        /// The loader inspects the byte order mark; each supported encoding must decode to the same chat, including non-ASCII text.
        /// </summary>
        [Theory]
        [InlineData("utf-8-bom")]
        [InlineData("utf-16le")]
        [InlineData("utf-16be")]
        [InlineData("utf-32le", Skip = "Bug: UTF-32 chat files are never detected: ArrayPool.Rent(4) returns a 16-byte buffer, so the exact-length UTF-32 BOM list patterns in ChatJson.GetJsonDocumentAsync never match (LE is mis-decoded as UTF-16, BE is rejected)")]
        [InlineData("utf-32be", Skip = "Bug: UTF-32 chat files are never detected: ArrayPool.Rent(4) returns a 16-byte buffer, so the exact-length UTF-32 BOM list patterns in ChatJson.GetJsonDocumentAsync never match (LE is mis-decoded as UTF-16, BE is rejected)")]
        public async Task Deserialize_WithByteOrderMark_DecodesEachSupportedEncoding(string encodingName)
        {
            Encoding encoding = encodingName switch
            {
                "utf-8-bom" => new UTF8Encoding(true),
                "utf-16le" => new UnicodeEncoding(false, true),
                "utf-16be" => new UnicodeEncoding(true, true),
                "utf-32le" => new UTF32Encoding(false, true),
                "utf-32be" => new UTF32Encoding(true, true),
                _ => throw new ArgumentOutOfRangeException(nameof(encodingName))
            };
            var text = await File.ReadAllTextAsync(Fixture(SAMPLE), TestContext.Current.CancellationToken);
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();

            var chat = await Load(WriteTemp(bytes, "encoded.json"));

            AssertIsSample(chat);
        }

        /// <summary>
        /// Hand-edited files may contain // comments and trailing commas. Both are explicitly allowed by the loader, so
        /// tightening the parser options would break files users have already fixed up by hand.
        /// </summary>
        [Fact]
        public async Task Deserialize_JsonWithCommentsAndTrailingCommas_IsAccepted()
        {
            var json = $$$"""
                {
                  // edited by hand
                  "streamer": {"name": "TestStreamer", "login": "teststreamer", "id": 100000001,},
                  "video": {"id": "1000000001", "start": 0, "end": 10, "length": 10,},
                  "comments": [{{{Comment(1, "100000001", "2", "viewer", "viewer", "hi")}}},],
                }
                """;

            var chat = await Load(WriteTemp(json));

            Assert.Equal("hi", Assert.Single(chat.comments).message.body);
            Assert.Equal("teststreamer", chat.streamer.login);
        }

        /// <summary>
        /// The chat-update page only needs the first and last comment to show the time range of huge files, so
        /// onlyFirstAndLastComments must return exactly those two (in order) instead of the full list.
        /// </summary>
        [Fact]
        public async Task Deserialize_OnlyFirstAndLastComments_ReturnsTwoBoundaryComments()
        {
            var chat = await Load(Fixture(SAMPLE), onlyFirstAndLastComments: true);

            Assert.Equal(2, chat.comments.Count);
            Assert.Equal("00000000-0000-4000-8000-000000000001", chat.comments[0]._id);
            Assert.Equal($"00000000-0000-4000-8000-{SAMPLE_COMMENT_COUNT:000000000000}", chat.comments[1]._id);
        }

        /// <summary>
        /// ChatUpdater reloads files with getEmbeds: false to avoid decoding megabytes of base64 images it will replace anyway.
        /// The embedded section must then be skipped entirely while comments are still loaded.
        /// </summary>
        [Fact]
        public async Task Deserialize_WithoutEmbeds_SkipsEmbeddedData()
        {
            var chat = await Load(Fixture(SAMPLE), getEmbeds: false);

            Assert.Null(chat.embeddedData);
            Assert.Equal(SAMPLE_COMMENT_COUNT, chat.comments.Count);
        }

        /// <summary>
        /// getComments: false is a public option meant to read only the metadata (streamer/video/embeds) of a chat file.
        /// It currently cannot work: comments stay null and UpgradeChatJson immediately enumerates them.
        /// </summary>
        [Fact(Skip = "Bug: ChatJson.DeserializeAsync(getComments: false) throws ArgumentNullException because UpgradeChatJson enumerates the null comments list")]
        public async Task Deserialize_WithoutComments_ReturnsMetadataOnly()
        {
            var chat = await Load(Fixture(SAMPLE), getComments: false);

            Assert.Null(chat.comments);
            Assert.Equal("teststreamer", chat.streamer.login);
            Assert.Equal(120, chat.video.length);
        }

        /// <summary>
        /// Chat 1.3.0 files did not store the streamer's login. On load it is looked up from Twitch by streamer id so renders
        /// and filename templates have it; the request must target the stored id.
        /// </summary>
        [Fact]
        public async Task Deserialize_V130_MissingStreamerLogin_IsFetchedFromTwitch()
        {
            RespondWithUser("100000001", "teststreamer", "TestStreamer");

            var chat = await Load(Fixture("chat_v1_3_0.json"));

            Assert.Equal("teststreamer", chat.streamer.login);
            Assert.Equal("TestStreamer", chat.streamer.name);
            var request = Assert.Single(_http.Requests);
            Assert.Contains("100000001", request.Body);
            Assert.Equal(new ChatRootVersion(1, 3, 0), chat.FileInfo.Version);
            Assert.Equal("Subscriber", chat.embeddedData.twitchBadges.Single().versions["0"].title);
        }

        /// <summary>
        /// The login lookup is best effort: when Twitch is unreachable (or the user is offline) a 1.3.0 chat must still load,
        /// just without the login, instead of failing the whole render.
        /// </summary>
        [Fact]
        public async Task Deserialize_V130_LoginLookupFails_StillLoads()
        {
            var chat = await Load(Fixture("chat_v1_3_0.json"));

            Assert.Null(chat.streamer.login);
            Assert.Equal(100000001, chat.streamer.id);
            Assert.Equal(3, chat.comments.Count);
            Assert.Single(_http.Requests);
        }

        /// <summary>
        /// Up to chat 1.2.2 an embedded badge version mapped directly to its image bytes; 1.3.0 changed it to an object with
        /// title/description/bytes. Older files must be converted on load so their embedded badges keep rendering.
        /// </summary>
        [Fact]
        public async Task Deserialize_V122_LegacyBadgeBytes_AreConvertedToBadgeData()
        {
            RespondWithUser("100000001", "teststreamer", "TestStreamer");

            var chat = await Load(Fixture("chat_v1_2_2.json"));

            var subscriber = Assert.Single(chat.embeddedData.twitchBadges);
            Assert.Equal("subscriber", subscriber.name);
            Assert.Equal(["0", "3"], subscriber.versions.Keys.Order());
            Assert.All(subscriber.versions.Values, v =>
            {
                Assert.Null(v.title);
                Assert.Equal(PngMagic, v.bytes[..4]);
            });
            Assert.Single(chat.embeddedData.firstParty);
            Assert.Equal(1, Assert.Single(chat.embeddedData.twitchBits).tierList.Keys.Single());
        }

        /// <summary>
        /// A pre-1.2.3 file whose embeddedData is null (images were not embedded) must load with empty embedded lists
        /// rather than throwing while converting the legacy badges.
        /// </summary>
        [Fact]
        public async Task Deserialize_LegacyNullEmbeddedData_YieldsEmptyLists()
        {
            var json = $$$"""
                {"FileInfo":{"Version":{"Major":1,"Minor":2,"Patch":0}},
                 "streamer":{"name":"TestStreamer","login":"teststreamer","id":100000001},
                 "video":{"id":"1000000001","start":0,"end":10,"length":10},
                 "comments":[{{{Comment(1, "100000001", "2", "viewer", "viewer", "hi")}}}],
                 "embeddedData":null}
                """;

            var chat = await Load(WriteTemp(json));

            Assert.Empty(chat.embeddedData.firstParty);
            Assert.Empty(chat.embeddedData.thirdParty);
            Assert.Empty(chat.embeddedData.twitchBadges);
            Assert.Empty(chat.embeddedData.twitchBits);
        }

        /// <summary>
        /// Chats from 2020-2022 have no FileInfo/streamer, store Twitch v5 video fields (user_id, user_name, duration "1h2m3s"),
        /// use "emotes" instead of "embeddedData", 300x300 avatars and never filled bits_spent. All of it must be upgraded on load:
        /// streamer rebuilt (login from the broadcaster's own comment, so no network), deprecated fields cleared, length parsed,
        /// bits recovered from "Cheer100" text and avatars shrunk to 70x70.
        /// </summary>
        [Fact]
        public async Task Deserialize_LegacyV5Chat_IsUpgradedToCurrentShape()
        {
            var chat = await Load(Fixture("chat_legacy_v5.json"));

            Assert.Equal(new ChatRootVersion(1, 0, 0), chat.FileInfo.Version);
            Assert.Equal(100000001, chat.streamer.id);
            Assert.Equal("TestStreamer", chat.streamer.name);
            Assert.Equal("teststreamer", chat.streamer.login);
            Assert.Empty(_http.Requests);

#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Null(chat.video.user_id);
            Assert.Null(chat.video.user_name);
            Assert.Null(chat.video.duration);
#pragma warning restore CS0618
            Assert.Equal(3723, chat.video.length); // 1h2m3s
            Assert.Equal(3723, chat.video.end);

            Assert.Equal("TestBttv", Assert.Single(chat.embeddedData.thirdParty).name);
            Assert.Equal("25", Assert.Single(chat.embeddedData.firstParty).id);
            Assert.Empty(chat.embeddedData.twitchBadges);

            Assert.Equal([0, 100, 0], chat.comments.Select(c => c.message.bits_spent));
            Assert.All(chat.comments.Where(c => c.commenter.logo is not null), c =>
            {
                Assert.Contains("70x70", c.commenter.logo);
                Assert.DoesNotContain("300x300", c.commenter.logo);
            });
        }

        /// <summary>
        /// The oldest chats contain only a comment list. The video block must be synthesized from the comments
        /// (start floored, end ceiled, id and creation time back-computed) and the streamer inferred from channel_id and the
        /// broadcaster-badged comment, so the renderer has a valid time range without a network call.
        /// </summary>
        [Fact]
        public async Task Deserialize_CommentsOnly_SynthesizesVideoAndStreamer()
        {
            var json = $$$"""
                {"comments":[
                  {{{Comment(10.4, "100000001", "100000011", "viewer_one", "viewer_one", "first", createdAt: "2024-01-01T00:00:10.4Z")}}},
                  {{{Comment(50, "100000001", "100000001", "teststreamer", "TestStreamer", "hello", badge: "broadcaster")}}},
                  {{{Comment(95.2, "100000001", "100000012", "viewer_two", "viewer_two", "last")}}}
                ]}
                """;

            var chat = await Load(WriteTemp(json));

            Assert.Equal(10, chat.video.start);
            Assert.Equal(96, chat.video.end);
            Assert.Equal("1000000001", chat.video.id);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), chat.video.created_at.ToUniversalTime());
            Assert.Equal(100000001, chat.streamer.id);
            Assert.Equal("TestStreamer", chat.streamer.name);
            Assert.Equal("teststreamer", chat.streamer.login);
            Assert.Empty(_http.Requests);
        }

        /// <summary>
        /// The chat downloader deliberately writes an empty channel_id when Twitch omits it. The streamer id must then fall
        /// back to the broadcaster-badged commenter's id instead of becoming 0.
        /// </summary>
        [Fact]
        public async Task Deserialize_EmptyChannelIds_FallsBackToBroadcasterCommenterId()
        {
            var json = $$$"""
                {"video":{"id":"1000000001","start":0,"end":60,"length":60},
                 "comments":[
                   {{{Comment(1, "", "100000011", "viewer_one", "viewer_one", "hi")}}},
                   {{{Comment(2, "", "100000001", "teststreamer", "TestStreamer", "welcome", badge: "broadcaster")}}}
                 ]}
                """;

            var chat = await Load(WriteTemp(json));

            Assert.Equal(100000001, chat.streamer.id);
            Assert.Equal("teststreamer", chat.streamer.login);
        }

        /// <summary>
        /// When the streamer id is known (from channel_id) but the broadcaster never chatted, name and login are looked up
        /// from Twitch exactly once.
        /// </summary>
        [Fact]
        public async Task Deserialize_UnknownStreamerName_IsFetchedOnceFromTwitch()
        {
            RespondWithUser("100000001", "teststreamer", "TestStreamer");
            var json = $$$"""{"comments":[{{{Comment(1, "100000001", "100000011", "viewer_one", "viewer_one", "hi")}}}]}""";

            var chat = await Load(WriteTemp(json));

            Assert.Equal(100000001, chat.streamer.id);
            Assert.Equal("TestStreamer", chat.streamer.name);
            Assert.Equal("teststreamer", chat.streamer.login);
            Assert.Single(_http.Requests);
        }

        /// <summary>
        /// If that name/login lookup fails (offline, Twitch down) the chat must still load with the inferred id, so old chats
        /// stay renderable without internet.
        /// </summary>
        [Fact]
        public async Task Deserialize_UnknownStreamerName_LookupFails_StillLoads()
        {
            var json = $$$"""{"comments":[{{{Comment(1, "100000001", "100000011", "viewer_one", "viewer_one", "hi")}}}]}""";

            var chat = await Load(WriteTemp(json));

            Assert.Equal(100000001, chat.streamer.id);
            Assert.Null(chat.streamer.name);
            Assert.Null(chat.streamer.login);
            Assert.Single(chat.comments);
        }

        /// <summary>
        /// With no way to infer the streamer (no ids, no broadcaster comment) the loader must settle for an empty streamer with
        /// id 0 and must not query Twitch for user id 0.
        /// </summary>
        [Fact]
        public async Task Deserialize_NoStreamerHints_UsesIdZeroWithoutNetwork()
        {
            var json = $$$"""{"comments":[{{{Comment(1, "", "100000011", "viewer_one", "viewer_one", "hi")}}}]}""";

            var chat = await Load(WriteTemp(json));

            Assert.Equal(0, chat.streamer.id);
            Assert.Null(chat.streamer.name);
            Assert.Null(chat.streamer.login);
            Assert.Empty(_http.Requests);
        }

        /// <summary>
        /// An empty chat with no video block gets a 0..48h range (Twitch's maximum broadcast length) so the renderer still
        /// has a valid, bounded time range.
        /// </summary>
        [Fact]
        public async Task Deserialize_NoCommentsNoVideo_DefaultsToMaxStreamLength()
        {
            var chat = await Load(WriteTemp("""{"comments":[]}"""));

            Assert.Equal(0, chat.video.start);
            Assert.Equal(172_800, chat.video.end);
            Assert.Null(chat.video.id);
            Assert.Equal(default, chat.video.created_at);
        }

        /// <summary>
        /// Very old chats stored the streamer id as a JSON string ("12345"). It must still parse into the int property
        /// (regression guard for lay295#843).
        /// </summary>
        [Fact]
        public async Task Deserialize_StringifiedStreamerId_IsParsed()
        {
            var json = $$$"""
                {"streamer":{"name":"TestStreamer","login":"teststreamer","id":"100000001"},
                 "video":{"id":"1000000001","start":0,"end":10,"length":10},
                 "comments":[{{{Comment(1, "100000001", "2", "viewer", "viewer", "hi")}}}]}
                """;

            var chat = await Load(WriteTemp(json));

            Assert.Equal(100000001, chat.streamer.id);
        }

        /// <summary>
        /// bits_spent is only recomputed from the message text when every comment has 0 (the broken v5-shutdown era).
        /// If any comment already carries bits the file is trusted as-is, so later cheers are not double counted or invented.
        /// </summary>
        [Fact]
        public async Task Deserialize_SomeBitsAlreadySet_DoesNotRecomputeBits()
        {
            var json = $$$"""
                {"streamer":{"name":"TestStreamer","login":"teststreamer","id":100000001},
                 "video":{"id":"1000000001","start":0,"end":10,"length":10},
                 "comments":[
                   {{{Comment(1, "100000001", "2", "viewer", "viewer", "Cheer50 hi", bits: 50)}}},
                   {{{Comment(2, "100000001", "3", "viewer2", "viewer2", "Cheer100 hi")}}}
                 ]}
                """;

            var chat = await Load(WriteTemp(json));

            Assert.Equal([50, 0], chat.comments.Select(c => c.message.bits_spent));
        }

        /// <summary>A null path is a programming error and must be reported as such, not as a missing file.</summary>
        [Fact]
        public async Task Deserialize_NullPath_ThrowsArgumentNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => Load(null!));
        }

        /// <summary>A missing file is reported as an IOException, which the GUI/CLI surface to the user.</summary>
        [Fact]
        public async Task Deserialize_MissingFile_ThrowsIOException()
        {
            await Assert.ThrowsAsync<IOException>(() => Load(_temp.Combine("does_not_exist.json")));
        }

        /// <summary>A file too short to hold a format signature must fail with a clear error instead of being parsed.</summary>
        [Fact]
        public async Task Deserialize_TooShortFile_ThrowsEndOfStream()
        {
            var ex = await Assert.ThrowsAsync<EndOfStreamException>(() => Load(WriteTemp("{}", "tiny.json")));
            Assert.Contains("tiny.json", ex.Message);
        }

        /// <summary>
        /// Picking a non-chat file (e.g. a video or text file) must fail with NotSupportedException naming the file, not
        /// with an obscure JSON parse error deep in the loader.
        /// </summary>
        [Fact]
        public async Task Deserialize_NotJson_ThrowsNotSupported()
        {
            var ex = await Assert.ThrowsAsync<NotSupportedException>(() => Load(WriteTemp("hello world", "notes.txt")));
            Assert.Contains("notes.txt", ex.Message);
        }

        /// <summary>
        /// A valid JSON document may start with whitespace (e.g. after being re-saved by some editors or tools).
        /// The format sniffing only accepts a leading '{' and therefore rejects such a file as "not a valid chat format".
        /// </summary>
        [Fact(Skip = "Bug: ChatJson rejects valid chat JSON that starts with whitespace/newline (format sniffing only accepts a leading '{')")]
        public async Task Deserialize_LeadingWhitespace_IsAccepted()
        {
            var json = "\n  " + await File.ReadAllTextAsync(Fixture(SAMPLE), TestContext.Current.CancellationToken);

            var chat = await Load(WriteTemp(json));

            AssertIsSample(chat);
        }

        /// <summary>Loading must honour cancellation so the GUI can abort a long load of a huge file.</summary>
        [Fact]
        public async Task Deserialize_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ChatJson.DeserializeAsync(Fixture(SAMPLE), cancellationToken: cts.Token));
        }

        /// <summary>
        /// Writing a loaded chat and loading it again must be lossless: a second serialization is byte-identical to the first.
        /// This is what ChatUpdater relies on when it rewrites files in place.
        /// </summary>
        [Fact]
        public async Task SerializeThenDeserialize_RoundTripsLosslessly()
        {
            var first = await Serialize(await Load(Fixture(SAMPLE)));

            var reloaded = await Load(WriteTemp(first, "roundtrip.json"));
            var second = await Serialize(reloaded);

            AssertIsSample(reloaded);
            Assert.Equal(first, second);
        }

        /// <summary>
        /// Pins the on-disk shape that other tools (and older TwitchDownloader versions) read: non-ASCII text and HTML-sensitive
        /// characters are written unescaped (only emoji outside the BMP get \u-escaped, which is still valid JSON), null-only
        /// fields (user_notice_params, badge url, isZeroWidth) are omitted, the chapter type is written as "type", and
        /// deprecated v5 video fields never reappear.
        /// </summary>
        [Fact]
        public async Task Serialize_WritesExpectedJsonShape()
        {
            var bytes = await Serialize(await Load(Fixture(SAMPLE)));
            var text = Encoding.UTF8.GetString(bytes);
            var root = JsonNode.Parse(bytes)!;

            Assert.Equal('{', (char)bytes[0]); // no BOM, so the file stays loadable by the '{' sniffing
            Assert.Contains("こんにちは ", text);
            Assert.Contains("<b>not bold</b> & \\\"quoted\\\"", text);

            Assert.Equal(4, root["FileInfo"]!["Version"]!["Minor"]!.GetValue<int>());
            Assert.Equal("GAME_CHANGE", root["video"]!["chapters"]![0]!["type"]!.GetValue<string>());
            Assert.False(root["video"]!.AsObject().ContainsKey("duration"));
            Assert.False(root["video"]!.AsObject().ContainsKey("user_id"));

            var messages = root["comments"]!.AsArray().Select(c => c!["message"]!.AsObject()).ToList();
            Assert.Single(messages, m => m.ContainsKey("user_notice_params"));

            var firstParty = root["embeddedData"]!["firstParty"]![0]!.AsObject();
            Assert.False(firstParty.ContainsKey("isZeroWidth"));
            Assert.False(firstParty.ContainsKey("url"));
            Assert.True(root["embeddedData"]!["thirdParty"]![0]!.AsObject().ContainsKey("isZeroWidth"));
            Assert.False(root["embeddedData"]!["twitchBadges"]![0]!["versions"]!["1"]!.AsObject().ContainsKey("url"));
        }

        /// <summary>
        /// Loading a legacy file and saving it (what "Update chat" does) must produce the current layout: "embeddedData"
        /// instead of "emotes", a streamer block, and no deprecated v5 video fields.
        /// </summary>
        [Fact]
        public async Task Serialize_UpgradedLegacyChat_WritesCurrentLayout()
        {
            var root = JsonNode.Parse(await Serialize(await Load(Fixture("chat_legacy_v5.json"))))!.AsObject();

            Assert.False(root.ContainsKey("emotes"));
            Assert.Equal("TestBttv", root["embeddedData"]!["thirdParty"]![0]!["name"]!.GetValue<string>());
            Assert.Equal("teststreamer", root["streamer"]!["login"]!.GetValue<string>());
            var video = root["video"]!.AsObject();
            Assert.False(video.ContainsKey("duration"));
            Assert.False(video.ContainsKey("user_id"));
            Assert.False(video.ContainsKey("user_name"));
            Assert.Equal(3723, video["length"]!.GetValue<double>());
        }
    }
}
