using System.Net;
using System.Text.Json;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper's first-party Twitch asset loaders: GetEmotes, GetChatBadgesData, GetChatBadges and GetBits.
    // They feed the chat renderer and the "embed images" option of chat downloads/updates: they must fetch exactly the assets
    // used in chat, prefer embedded data, respect offline mode and skip (not crash on) assets Twitch has deleted.
    [Collection(StaticHttpClientCollection.NAME)]
    public class TwitchHelperFirstPartyAssetTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static string GqlQuery(FakeHttpMessageHandler.RecordedRequest request) => JsonDocument.Parse(request.Body!).RootElement.GetProperty("query").GetString()!;

        private static string EmoteUrl(string id) => $"https://static-cdn.jtvnw.net/emoticons/v2/{id}/default/dark/2.0";

        // ---------------- GetEmotes ----------------

        // Every distinct emote id referenced by chat fragments must be downloaded exactly once and cached under emotes/{id}_2.png;
        // comments without fragments and text-only fragments are ignored.
        [Fact]
        public async Task GetEmotes_DownloadsEachReferencedEmoteOnce()
        {
            var handler = Routes((EmoteUrl("25"), Bytes(Png())), (EmoteUrl("1902"), Bytes(Png())));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var textOnly = Comment("plain text");
            textOnly.message.fragments = [new() { text = "plain text", emoticon = null }];
            List<Comment> comments = [Comment("Kappa Keepo", emoteIds: ["25", "1902"]), Comment("Kappa", emoteIds: ["25"]), Comment("no fragments"), textOnly];

            var emotes = await TwitchHelper.GetEmotes(comments, cache.Path, new RecordingTaskProgress(), cancellationToken: Ct);

            Assert.Equal(["1902", "25"], emotes.Select(e => e.Id).Order());
            Assert.All(emotes, e => Assert.Equal((e.Id, EmoteProvider.FirstParty, 2), (e.Name, e.EmoteProvider, e.ImageScale)));
            Assert.Equal(2, handler.Requests.Count);
            Assert.True(File.Exists(Path.Combine(cache.Path, "emotes", "25_2.png")));
        }

        // Emotes Twitch has deleted (404) must simply be left out.
        [Fact]
        public async Task GetEmotes_DeletedEmote_IsSkipped()
        {
            using var _ = UseHandler(Routes((EmoteUrl("25"), Bytes(Png()))));
            using var cache = new TempDirectory();

            var emotes = await TwitchHelper.GetEmotes([Comment("x", emoteIds: ["25", "404404"])], cache.Path, new RecordingTaskProgress(), cancellationToken: Ct);

            Assert.Equal("25", Assert.Single(emotes).Id);
        }

        private static EmbeddedData EmbeddedEmotes() => new()
        {
            firstParty =
            [
                new EmbedEmoteData { id = "301", name = "EmbeddedEmote", imageScale = 2, data = Png(9, 9) },
                new EmbedEmoteData { id = "301", name = "DuplicateEmote", imageScale = 2, data = Png() },
                new EmbedEmoteData { id = "666", name = "BrokenEmote", imageScale = 2, data = NotAnImage },
            ]
        };

        // Offline renders must use embedded emotes (logging duplicates/undecodable entries) and never touch the network for uncached ones.
        [Fact]
        public async Task GetEmotes_OfflineWithEmbeddedData_UsesEmbeddedOnly()
        {
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var emotes = await TwitchHelper.GetEmotes([Comment("x", emoteIds: ["301", "999"])], cache.Path, progress, EmbeddedEmotes(), offline: true, cancellationToken: Ct);

            var emote = Assert.Single(emotes);
            Assert.Equal(("301", "EmbeddedEmote", 9), (emote.Id, emote.Name, emote.Width));
            Assert.Empty(handler.Requests);
            Assert.Contains("Tried to add duplicate emote from embedded data: DuplicateEmote.", progress.Verbose);
            Assert.Contains(progress.Verbose, v => v.StartsWith("An exception occurred while loading embedded emote 'BrokenEmote'"));
        }

        // Online, the embedded copy of an emote must win over any downloaded copy (the embedded image is what the chat was archived with).
        [Fact]
        public async Task GetEmotes_OnlineWithEmbeddedData_EmbeddedCopyWins()
        {
            using var _ = UseHandler(Routes((EmoteUrl("301"), Bytes(Png())), (EmoteUrl("25"), Bytes(Png()))));
            using var cache = new TempDirectory();

            var emotes = await TwitchHelper.GetEmotes([Comment("x", emoteIds: ["301", "25"])], cache.Path, new RecordingTaskProgress(), EmbeddedEmotes(), cancellationToken: Ct);

            Assert.Equal(["25", "EmbeddedEmote"], emotes.Select(e => e.Name).Order());
            Assert.Equal(9, Assert.Single(emotes, e => e.Id == "301").Width);
        }

        // Emotes already present in the embedded data must not be downloaded again (they used to be skipped before #1475; the
        // "should never occur" duplicate branch shows that is still the intent). Re-downloading wastes requests and can fail renders of old chats.
        [Fact(Skip = "Bug: GetEmotes no longer skips emote ids already loaded from embedded data (regressed in eb717ab/#1475), so embedded first-party emotes are re-downloaded and then discarded")]
        public async Task GetEmotes_EmbeddedEmote_IsNotRedownloaded()
        {
            var handler = Routes((EmoteUrl("301"), Bytes(Png())));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();

            var emotes = await TwitchHelper.GetEmotes([Comment("x", emoteIds: ["301"])], cache.Path, new RecordingTaskProgress(), EmbeddedEmotes(), cancellationToken: Ct);

            Assert.Equal("EmbeddedEmote", Assert.Single(emotes).Name);
            Assert.Empty(handler.Requests);
        }

        // Cancellation must stop emote loading promptly.
        [Fact]
        public async Task GetEmotes_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            using var cache = new TempDirectory();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                TwitchHelper.GetEmotes([Comment("x", emoteIds: ["25"])], cache.Path, new RecordingTaskProgress(), cancellationToken: cts.Token));
        }

        // ---------------- Badges ----------------

        private const string BROADCASTER_IMG = "https://static-cdn.jtvnw.net/badges/v1/5527c58c-fb7d-422d-b71b-f309dcb85cc1/2";
        private const string SUB0_IMG = "https://static-cdn.jtvnw.net/badges/v1/aaaaaaaa-0000-0000-0000-000000000000/2";
        private const string SUB3_IMG = "https://static-cdn.jtvnw.net/badges/v1/aaaaaaaa-0000-0000-0000-000000000003/2";

        private const string SUB_GLOBAL_IMG = "https://static-cdn.jtvnw.net/badges/v1/5d9f2208-5dd8-11e7-8513-2ff4adfae661/2";

        private static object Badge(string imageURL, string title, string setID, string version) => new { imageURL, description = title, title, setID, version };

        private static readonly string GLOBAL_BADGES = Ser(new { data = new { badges = new[]
        {
            Badge(BROADCASTER_IMG, "Broadcaster", "broadcaster", "1"),
            Badge(SUB_GLOBAL_IMG, "Subscriber", "subscriber", "0"),
            Badge(SUB_GLOBAL_IMG, "Subscriber", "subscriber", "1"),
            Badge("https://static-cdn.jtvnw.net/badges/v1/3267646d-33f0-4b17-b3df-f923a41db1d0/2", "Moderator", "moderator", "1"),
        } } });

        private static readonly string SUB_BADGES = Ser(new { data = new { user = new { broadcastBadges = new[]
        {
            Badge(SUB0_IMG, "Channel Subscriber", "subscriber", "0"),
            Badge(SUB3_IMG, "3-Month Subscriber", "subscriber", "3"),
        } } } });

        // Answers the two badge GQL queries (told apart by their body) and serves the given badge images; everything else is 404.
        private static FakeHttpMessageHandler BadgeHandler(params string[] imageUrls) => new((req, body) =>
            req.RequestUri!.AbsoluteUri == GQL_URL
                ? FakeHttpMessageHandler.Json(body!.Contains("broadcastBadges") ? SUB_BADGES : GLOBAL_BADGES)
                : imageUrls.Contains(req.RequestUri.AbsoluteUri) ? FakeHttpMessageHandler.Bytes(Png()) : new HttpResponseMessage(HttpStatusCode.NotFound));

        private static List<Comment> BadgeComments()
        {
            var noBadges = Comment("no badges");
            return [Comment("a", badgeIds: ["broadcaster", "subscriber"]), Comment("b", badgeIds: ["subscriber", "", "not-a-real-badge"]), noBadges];
        }

        // Only badge sets actually worn in chat may be returned; channel (sub) badges must override global versions with the same key,
        // and the channel query must target the streamer id.
        [Fact]
        public async Task GetChatBadgesData_MergesGlobalAndChannelBadgesForUsedSets()
        {
            var handler = BadgeHandler();
            using var _ = UseHandler(handler);

            var badges = await TwitchHelper.GetChatBadgesData(BadgeComments(), STREAMER, Ct);

            Assert.Equal(["broadcaster", "subscriber"], badges.Select(b => b.name));
            var sub = badges[1].versions;
            Assert.Equal(["0", "1", "3"], sub.Keys.Order());
            Assert.Equal(("Channel Subscriber", SUB0_IMG), (sub["0"].title, sub["0"].url)); // channel overrides global "0"
            Assert.Equal("Subscriber", sub["1"].title); // global-only version kept
            Assert.Equal(BROADCASTER_IMG, badges[0].versions["1"].url);

            var requests = handler.Requests.ToArray();
            Assert.Equal(2, requests.Length);
            Assert.All(requests, r => Assert.Equal(WEB_CLIENT_ID, r.Headers["Client-ID"]));
            Assert.Contains($"user(id: {STREAMER})", GqlQuery(requests[1]));
        }

        // A failing badge query must throw (the badge list is required for a correct render), not return an empty list.
        [Fact]
        public async Task GetChatBadgesData_HttpError_Throws()
        {
            using var _ = UseHandler(new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway)));

            await Assert.ThrowsAsync<HttpRequestException>(() => TwitchHelper.GetChatBadgesData(BadgeComments(), STREAMER, Ct));
        }

        // Badge images must be downloaded per version into badges/{badge uuid}_2.png and wrapped in ChatBadge with titles and the badge type.
        [Fact]
        public async Task GetChatBadges_DownloadsVersionImagesAndBuildsBadges()
        {
            using var _ = UseHandler(BadgeHandler(BROADCASTER_IMG, SUB0_IMG, SUB3_IMG, SUB_GLOBAL_IMG));
            using var cache = new TempDirectory();

            var badges = await TwitchHelper.GetChatBadges(BadgeComments(), STREAMER, cache.Path, new RecordingTaskProgress(), cancellationToken: Ct);

            var sub = Assert.Single(badges, b => b.Name == "subscriber");
            Assert.Equal(ChatBadgeType.Subscriber, sub.Type);
            Assert.Equal("3-Month Subscriber", sub.VersionsData["3"].title);
            Assert.NotEmpty(sub.VersionsData["3"].bytes);
            Assert.Equal(ChatBadgeType.Broadcaster, Assert.Single(badges, b => b.Name == "broadcaster").Type);
            Assert.True(File.Exists(Path.Combine(cache.Path, "badges", "5527c58c-fb7d-422d-b71b-f309dcb85cc1_2.png")));
        }

        // If any version image of a badge is gone (404) that badge is left out, but the others still load.
        [Fact]
        public async Task GetChatBadges_MissingVersionImage_SkipsThatBadgeOnly()
        {
            using var _ = UseHandler(BadgeHandler(BROADCASTER_IMG, SUB0_IMG)); // subscriber v1 and v3 images 404
            using var cache = new TempDirectory();

            var badges = await TwitchHelper.GetChatBadges(BadgeComments(), STREAMER, cache.Path, new RecordingTaskProgress(), cancellationToken: Ct);

            Assert.Equal("broadcaster", Assert.Single(badges).Name);
        }

        private static EmbeddedData EmbeddedBadges() => new()
        {
            twitchBadges =
            [
                new EmbedChatBadge { name = "broadcaster", versions = new() { ["1"] = new ChatBadgeData { title = "Embedded Broadcaster", description = "d", bytes = Png(9, 9) } } },
                new EmbedChatBadge { name = "broadcaster", versions = new() { ["1"] = new ChatBadgeData { title = "Duplicate", description = "d", bytes = Png() } } },
                new EmbedChatBadge { name = "broken", versions = new() { ["1"] = new ChatBadgeData { title = "Broken", description = "d", bytes = NotAnImage } } },
            ]
        };

        // Embedded badges must win over downloads (no request for their images); duplicates/undecodable entries are logged and skipped.
        [Fact]
        public async Task GetChatBadges_EmbeddedBadges_AreNotRedownloaded()
        {
            var handler = BadgeHandler(BROADCASTER_IMG, SUB0_IMG, SUB3_IMG, "https://static-cdn.jtvnw.net/badges/v1/5d9f2208-5dd8-11e7-8513-2ff4adfae661/2");
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var badges = await TwitchHelper.GetChatBadges(BadgeComments(), STREAMER, cache.Path, progress, EmbeddedBadges(), cancellationToken: Ct);

            Assert.Equal("Embedded Broadcaster", Assert.Single(badges, b => b.Name == "broadcaster").VersionsData["1"].title);
            Assert.Contains(badges, b => b.Name == "subscriber");
            Assert.DoesNotContain(BROADCASTER_IMG, RequestedUrls(handler));
            Assert.Contains("Tried to add duplicate badge from embedded data: broadcaster.", progress.Verbose);
            Assert.Contains(progress.Verbose, v => v.StartsWith("An exception occurred while loading embedded badge 'broken'"));
        }

        // Offline mode must return only the embedded badges and make no request at all (not even the badge GQL query).
        [Fact]
        public async Task GetChatBadges_Offline_ReturnsEmbeddedWithoutRequests()
        {
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();

            var badges = await TwitchHelper.GetChatBadges(BadgeComments(), STREAMER, cache.Path, new RecordingTaskProgress(), EmbeddedBadges(), offline: true, cancellationToken: Ct);

            Assert.Equal("broadcaster", Assert.Single(badges).Name);
            Assert.Empty(handler.Requests);
        }

        // ---------------- Bits / cheermotes ----------------

        private const string TEMPLATE = "https://d3aqoihi2n8ty8.cloudfront.net/actions/PREFIX/BACKGROUND/ANIMATION/TIER/SCALE.EXTENSION";
        private const string CHEER1 = "https://d3aqoihi2n8ty8.cloudfront.net/actions/cheer/dark/animated/1/2.gif";
        private const string CHEER100 = "https://d3aqoihi2n8ty8.cloudfront.net/actions/cheer/dark/animated/100/2.gif";
        private const string MYCHEER1 = "https://d3aqoihi2n8ty8.cloudfront.net/partner-actions/123/mycheer/dark/animated/1/2.gif";

        private static object CheerGroup(string templateURL, params (string id, string prefix, int[] tiers)[] nodes) =>
            new { nodes = nodes.Select(n => new { n.id, n.prefix, tiers = n.tiers.Select(bits => new { bits }) }), templateURL };

        private static readonly object GLOBAL_CHEER_GROUP = CheerGroup(TEMPLATE, ("cheer-node", "Cheer", [1, 100]), ("bible-node", "BibleThump", [1]), ("unused-node", "Unused", [1]));

        private static readonly string CHEER_JSON = Ser(new { data = new
        {
            cheerConfig = new { groups = new[] { GLOBAL_CHEER_GROUP } },
            user = new { cheer = new { cheerGroups = new[] { CheerGroup("https://d3aqoihi2n8ty8.cloudfront.net/partner-actions/123/PREFIX/BACKGROUND/ANIMATION/TIER/SCALE.EXTENSION", ("mycheer-node", "MyCheer", [1])) } } }
        } });

        // Only real cheers (bits_spent > 0, prefix at a word start followed by a number) may trigger downloads.
        private static List<Comment> CheerComments() =>
        [
            Comment("Cheer100 great stream", bitsSpent: 100),
            Comment("MyCheer1", bitsSpent: 1),
            Comment("Unused100 typed without cheering", bitsSpent: 0),
            Comment("xBibleThump1 BibleThump", bitsSpent: 1),
        ];

        // Global and channel cheermote groups must both be considered; each used prefix gets every tier, fetched from the template URL
        // (lower-case prefix, dark, animated, tier, 2.gif) and cached as bits/{node id}{tier}_2.gif.
        [Fact]
        public async Task GetBits_DownloadsTiersOfUsedCheermotesOnly()
        {
            var handler = new FakeHttpMessageHandler((req, _) => req.RequestUri!.AbsoluteUri switch
            {
                GQL_URL => FakeHttpMessageHandler.Json(CHEER_JSON),
                CHEER1 or CHEER100 or MYCHEER1 => FakeHttpMessageHandler.Bytes(Gif()),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            });
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();

            var bits = await TwitchHelper.GetBits(CheerComments(), cache.Path, "123", new RecordingTaskProgress(), cancellationToken: Ct);

            Assert.Equal(["Cheer", "MyCheer"], bits.Select(b => b.prefix).Order());
            var cheer = Assert.Single(bits, b => b.prefix == "Cheer");
            Assert.Equal([1, 100], cheer.tierList.Select(t => t.Key));
            Assert.Equal(["Cheer1", "Cheer100"], cheer.tierList.Select(t => t.Value.Name));
            var requests = handler.Requests.ToArray();
            Assert.Contains("user(id:\"123\")", GqlQuery(requests[0]));
            Assert.Equal(WEB_CLIENT_ID, requests[0].Headers["Client-ID"]);
            Assert.Equal([CHEER1, CHEER100, MYCHEER1], requests.Skip(1).Select(r => r.Uri.AbsoluteUri));
            Assert.True(File.Exists(Path.Combine(cache.Path, "bits", "cheer-node100_2.gif")));
        }

        // Channels without custom cheermotes ("user": null) and responses without data must not crash.
        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 0)]
        public async Task GetBits_MissingChannelOrData_ReturnsWhatIsAvailable(bool hasData, int expectedCount)
        {
            var json = hasData ? Ser(new { data = new { cheerConfig = new { groups = new[] { GLOBAL_CHEER_GROUP } }, user = (object?)null } }) : """{"data":null}""";
            using var _ = UseHandler(new FakeHttpMessageHandler((req, _) => req.RequestUri!.AbsoluteUri == GQL_URL
                ? FakeHttpMessageHandler.Json(json)
                : FakeHttpMessageHandler.Bytes(Gif())));
            using var cache = new TempDirectory();

            var bits = await TwitchHelper.GetBits(CheerComments(), cache.Path, "123", new RecordingTaskProgress(), cancellationToken: Ct);

            Assert.Equal(expectedCount, bits.Count);
        }

        // If any tier image of a cheermote is gone (404) that cheermote is left out; other cheermotes still load.
        [Fact]
        public async Task GetBits_MissingTierImage_SkipsThatCheermoteOnly()
        {
            using var _ = UseHandler(new FakeHttpMessageHandler((req, _) => req.RequestUri!.AbsoluteUri switch
            {
                GQL_URL => FakeHttpMessageHandler.Json(CHEER_JSON),
                CHEER1 or MYCHEER1 => FakeHttpMessageHandler.Bytes(Gif()),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound) // CHEER100 is gone
            }));
            using var cache = new TempDirectory();

            var bits = await TwitchHelper.GetBits(CheerComments(), cache.Path, "123", new RecordingTaskProgress(), cancellationToken: Ct);

            Assert.Equal("MyCheer", Assert.Single(bits).prefix);
        }

        private static EmbeddedData EmbeddedBits() => new()
        {
            twitchBits =
            [
                new EmbedCheerEmote { prefix = "Cheer", tierList = new() { [1] = new EmbedEmoteData { id = "c1", name = "Cheer1", imageScale = 2, data = Gif() }, [100] = new EmbedEmoteData { id = "c100", name = "Cheer100", imageScale = 2, data = Gif() } } },
                new EmbedCheerEmote { prefix = "Cheer", tierList = new() { [1] = new EmbedEmoteData { id = "dup", name = "Dup", imageScale = 2, data = Gif() } } },
                new EmbedCheerEmote { prefix = "Broken", tierList = new() { [1] = new EmbedEmoteData { id = "b", name = "Broken1", imageScale = 2, data = NotAnImage } } },
            ]
        };

        // Offline mode must return only embedded cheermotes (with all tiers) and make no request; duplicates/undecodable entries are logged.
        [Fact]
        public async Task GetBits_Offline_ReturnsEmbeddedWithoutRequests()
        {
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var bits = await TwitchHelper.GetBits(CheerComments(), cache.Path, "123", progress, EmbeddedBits(), offline: true, cancellationToken: Ct);

            Assert.Equal([1, 100], Assert.Single(bits).tierList.Select(t => t.Key));
            Assert.Empty(handler.Requests);
            Assert.Contains("Tried to add duplicate cheermote from embedded data: Cheer.", progress.Verbose);
            Assert.Contains(progress.Verbose, v => v.StartsWith("An exception occurred while loading embedded cheermote 'Broken'"));
        }

        // Online, embedded cheermotes must not be downloaded again; only the missing ones are fetched.
        [Fact]
        public async Task GetBits_EmbeddedCheermotes_AreNotRedownloaded()
        {
            var handler = new FakeHttpMessageHandler((req, _) => req.RequestUri!.AbsoluteUri == GQL_URL ? FakeHttpMessageHandler.Json(CHEER_JSON) : FakeHttpMessageHandler.Bytes(Gif()));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();

            var bits = await TwitchHelper.GetBits(CheerComments(), cache.Path, "123", new RecordingTaskProgress(), EmbeddedBits(), cancellationToken: Ct);

            Assert.Equal(["Cheer", "MyCheer"], bits.Select(b => b.prefix).Order());
            Assert.Equal([GQL_URL, MYCHEER1], RequestedUrls(handler));
        }
    }
}
