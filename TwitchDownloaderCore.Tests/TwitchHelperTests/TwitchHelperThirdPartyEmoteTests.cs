using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper.GetThirdPartyEmotes, which turns BTTV/FFZ/7TV metadata into decoded TwitchEmote images for chat
    // renders and embedded chat JSON. It decides which emotes to download (only those used in chat), reuses embedded and
    // cached images, and must keep going when a single provider's CDN misbehaves.
    [Collection(StaticHttpClientCollection.NAME)]
    public class TwitchHelperThirdPartyEmoteTests
    {
        private const string BTTV_IMAGE_B1 = "https://cdn.betterttv.net/emote/b1/2x";

        private static Task<List<TwitchEmote>> Get(List<Comment>? comments, string cacheFolder, RecordingTaskProgress logger, EmbeddedData? embedded = null,
            bool bttv = true, bool ffz = true, bool stv = true, bool offline = false, CancellationToken? ct = null) =>
            TwitchHelper.GetThirdPartyEmotes(comments!, STREAMER, cacheFolder, logger, embedded!, bttv, ffz, stv, true, offline, ct ?? TestContext.Current.CancellationToken);

        private static FakeHttpMessageHandler BttvOnly(params (string Url, Func<HttpResponseMessage> Respond)[] extra) =>
            Routes([(BTTV_GLOBAL, Json(Ser(new[] { Bttv("b1", "UsedEmote"), Bttv("b2", "UnusedEmote"), Bttv("b3", "Kap") }))), .. extra]);

        // With every provider disabled nothing may be requested or created.
        [Fact]
        public async Task AllProvidersDisabled_ReturnsEmptyWithoutRequests()
        {
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();

            var emotes = await Get([], cache.Path, new RecordingTaskProgress(), bttv: false, ffz: false, stv: false);

            Assert.Empty(emotes);
            Assert.Empty(handler.Requests);
        }

        // Only emotes that appear as whole words in chat may be downloaded (a channel can have thousands); the image must be cached
        // on disk and the next run must load it from the cache without a network request.
        [Fact]
        public async Task DownloadsOnlyEmotesUsedInChat_ThenServesThemFromCache()
        {
            using var cache = new TempDirectory();
            List<Comment> comments = [Comment("hello UsedEmote world"), Comment("Kappa KapX notKap")];
            var firstHandler = BttvOnly((BTTV_IMAGE_B1, Bytes(Png())));
            var firstProgress = new RecordingTaskProgress();

            List<TwitchEmote> first;
            using (UseHandler(firstHandler))
            {
                first = await Get(comments, cache.Path, firstProgress, ffz: false, stv: false);
            }

            var emote = Assert.Single(first);
            Assert.Equal(("UsedEmote", "b1", 2, EmoteProvider.ThirdParty), (emote.Name, emote.Id, emote.ImageScale, emote.EmoteProvider));
            Assert.Equal(4, emote.Width);
            Assert.Equal([BTTV_GLOBAL, BTTV_CHANNEL, BTTV_IMAGE_B1], RequestedUrls(firstHandler));
            Assert.True(File.Exists(Path.Combine(cache.Path, "bttv", "b1_2.png")));
            Assert.Contains("Loaded 1 BetterTTV emotes from cache/download - 0 cached, 1 newly downloaded.", firstProgress.Info);

            var secondHandler = BttvOnly();
            var secondProgress = new RecordingTaskProgress();
            using (UseHandler(secondHandler))
            {
                Assert.Equal("UsedEmote", Assert.Single(await Get(comments, cache.Path, secondProgress, ffz: false, stv: false)).Name);
            }

            Assert.DoesNotContain(BTTV_IMAGE_B1, RequestedUrls(secondHandler));
            Assert.Contains("Loaded 1 BetterTTV emotes from cache/download - 1 cached, 0 newly downloaded.", secondProgress.Info);
        }

        // With no comments (e.g. "embed all emotes" in the chat updater) every emote of every provider must be fetched, using each provider's
        // [scale]=2 URL, and the metadata's zero-width flag must carry over to the TwitchEmote.
        [Fact]
        public async Task NullComments_FetchesEveryEmoteFromEveryProvider()
        {
            var handler = Routes(
                (BTTV_GLOBAL, Json(Ser(new[] { Bttv("b1", "BttvEmote") }))),
                (FFZ_GLOBAL, Json(Ser(new[] { Ffz(10, "FfzEmote") }))),
                (STV_GLOBAL, Json(StvSet(Stv("01A", "StvEmote", StvEmoteFlags.ZeroWidth)))),
                (BTTV_IMAGE_B1, Bytes(Png())),
                ("https://cdn.betterttv.net/frankerfacez_emote/10/2", Bytes(Png())),
                ("https://cdn.7tv.app/emote/01A/2x.webp", Bytes(Webp())));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();

            var emotes = await Get(null, cache.Path, new RecordingTaskProgress());

            Assert.Equal(["BttvEmote", "FfzEmote", "StvEmote"], emotes.Select(e => e.Name));
            Assert.Equal([false, false, true], emotes.Select(e => e.IsZeroWidth));
            Assert.True(File.Exists(Path.Combine(cache.Path, "stv", "01A_2.webp")));
        }

        // Emotes embedded in the chat JSON must be used as-is (keeping their zero-width flag) and must not be downloaded again;
        // duplicate or undecodable embedded entries must be logged and skipped rather than failing the render.
        [Fact]
        public async Task EmbeddedEmotes_AreUsedAndNotRefetched()
        {
            var embedded = new EmbeddedData
            {
                thirdParty =
                [
                    new EmbedEmoteData { id = "embedded1", name = "UsedEmote", imageScale = 2, data = Png(8, 8), isZeroWidth = true },
                    new EmbedEmoteData { id = "embedded2", name = "UsedEmote", imageScale = 2, data = Png() },
                    new EmbedEmoteData { id = "broken", name = "Broken", imageScale = 1, data = NotAnImage },
                ]
            };
            var handler = BttvOnly((BTTV_IMAGE_B1, Bytes(Png())));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var emotes = await Get([Comment("UsedEmote")], cache.Path, progress, embedded, ffz: false, stv: false);

            var emote = Assert.Single(emotes);
            Assert.Equal(("embedded1", true, 8), (emote.Id, emote.IsZeroWidth, emote.Width));
            Assert.DoesNotContain(BTTV_IMAGE_B1, RequestedUrls(handler));
            Assert.Contains("Tried to add duplicate emote from embedded data: UsedEmote.", progress.Verbose);
            Assert.Contains(progress.Verbose, v => v.StartsWith("An exception occurred while loading embedded emote 'Broken'"));
        }

        // A deleted emote image (404) is common; it must be a warning naming the emote and URL, not an error.
        [Fact]
        public async Task ImageNotFound_LogsWarningAndSkipsEmote()
        {
            using var _ = UseHandler(BttvOnly());
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var emotes = await Get([Comment("UsedEmote")], cache.Path, progress, ffz: false, stv: false);

            Assert.Empty(emotes);
            Assert.Contains($"Got 404: NotFound when fetching UsedEmote ({BTTV_IMAGE_B1}).", progress.Warnings);
            Assert.Empty(progress.Errors);
        }

        // Any other image failure (server error, TLS, timeout, unexpected exception) must be logged once with a provider-specific message,
        // and the other providers' emotes must still load.
        [Theory]
        [InlineData(FAIL_STATUS)]
        [InlineData(FAIL_SSL)]
        [InlineData(FAIL_TIMEOUT)]
        [InlineData(FAIL_OTHER)]
        public async Task ImageFailure_IsLoggedAndOtherProvidersStillLoad(string kind)
        {
            var handler = Failing(kind, uri => uri.AbsoluteUri == BTTV_IMAGE_B1,
                (BTTV_GLOBAL, Json(Ser(new[] { Bttv("b1", "UsedEmote") }))),
                (FFZ_GLOBAL, Json(Ser(new[] { Ffz(10, "FfzEmote") }))),
                ("https://cdn.betterttv.net/frankerfacez_emote/10/2", Bytes(Png())));
            using var _ = UseHandler(handler, kind == FAIL_TIMEOUT ? TimeSpan.FromMilliseconds(100) : null);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var emotes = await Get([Comment("UsedEmote FfzEmote")], cache.Path, progress, stv: false);

            Assert.Equal("FfzEmote", Assert.Single(emotes).Name);
            Assert.Equal($"{ExpectedFailureMessage(kind, "BetterTTV")} Some BetterTTV emotes may not be present for this session.", Assert.Single(progress.Errors));
        }

        // Each provider's image failures are isolated and attributed to that provider, so the log tells the user exactly which emotes may be missing.
        [Fact]
        public async Task ImageFailure_InEveryProvider_IsLoggedPerProvider()
        {
            var handler = Failing(FAIL_STATUS, uri => uri.Host.StartsWith("cdn."),
                (BTTV_GLOBAL, Json(Ser(new[] { Bttv("b1", "BttvEmote") }))),
                (FFZ_GLOBAL, Json(Ser(new[] { Ffz(10, "FfzEmote") }))),
                (STV_GLOBAL, Json(StvSet(Stv("01A", "StvEmote")))));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var emotes = await Get(null, cache.Path, progress);

            Assert.Empty(emotes);
            Assert.Equal(
                ["BetterTTV returned 500: InternalServerError. Some BetterTTV emotes may not be present for this session.",
                 "FFZ returned 500: InternalServerError. Some FFZ emotes may not be present for this session.",
                 "7TV returned 500: InternalServerError. Some 7TV emotes may not be present for this session."],
                progress.Errors);
        }

        // Offline renders must work purely from the metadata + image caches: cached images load, uncached ones are silently skipped, nothing hits the network.
        [Fact]
        public async Task Offline_UsesCachedImagesAndSkipsUncachedOnes()
        {
            using var cache = new TempDirectory();
            WriteCache(Path.Combine(cache.Path, "bttv", $"emotes_{STREAMER}.json.gz"), Item("c1", "CachedEmote"), Item("c2", "UncachedEmote"));
            await File.WriteAllBytesAsync(Path.Combine(cache.Path, "bttv", "c1_2.png"), Png(), TestContext.Current.CancellationToken);
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);

            var emotes = await Get([Comment("CachedEmote UncachedEmote")], cache.Path, new RecordingTaskProgress(), ffz: false, stv: false, offline: true);

            Assert.Equal("CachedEmote", Assert.Single(emotes).Name);
            Assert.Empty(handler.Requests);
        }

        // Cancelling while embedded emotes are being decoded must stop immediately.
        [Fact]
        public async Task CancelledToken_ThrowsWhileLoadingEmbeddedEmotes()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            using var cache = new TempDirectory();
            var embedded = new EmbeddedData { thirdParty = [new EmbedEmoteData { id = "e", name = "E", imageScale = 1, data = Png() }] };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Get([], cache.Path, new RecordingTaskProgress(), embedded, ct: cts.Token));
        }
    }
}
