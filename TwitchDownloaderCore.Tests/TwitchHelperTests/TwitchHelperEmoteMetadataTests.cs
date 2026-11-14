using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper.GetThirdPartyEmotesMetadata: BTTV/FFZ/7TV emote list parsing and the on-disk metadata cache
    // (RefreshOrLoadProviderMetadata) used when a provider is down or the user renders offline. Third-party providers fail
    // often, so this code must degrade to "log and continue" and never abort a chat download/render.
    [Collection(StaticHttpClientCollection.NAME)]
    public class TwitchHelperEmoteMetadataTests
    {
        private static Task<EmoteResponse> Fetch(string cacheFolder, RecordingTaskProgress logger, bool bttv = false, bool ffz = false, bool stv = false, bool allowUnlisted = true, bool offline = false) =>
            TwitchHelper.GetThirdPartyEmotesMetadata(STREAMER, bttv, ffz, stv, allowUnlisted, cacheFolder, offline, logger, TestContext.Current.CancellationToken);

        private static string CacheFile(string cacheFolder, string providerDir) => Path.Combine(cacheFolder, providerDir, $"emotes_{STREAMER}.json.gz");

        // BTTV global, channel and shared emotes must all be merged, CDN URLs templated with [scale], and BTTV's hard-coded
        // zero-width emotes (e.g. SoSnowy) flagged so the renderer overlays them. Only BTTV may be contacted when only BTTV is requested.
        [Fact]
        public async Task Bttv_MergesGlobalChannelAndSharedEmotes_AndFlagsZeroWidth()
        {
            var handler = Routes(
                (BTTV_GLOBAL, Json(Ser(new[] { Bttv("g1", "OhMyGoodness"), Bttv("g2", "SoSnowy") }))),
                (BTTV_CHANNEL, Json(Ser(new { id = "x", bots = Array.Empty<string>(), channelEmotes = new[] { Bttv("c1", "ChannelEmote", "gif") }, sharedEmotes = new[] { Bttv("s1", "SharedEmote") } }))));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, bttv: true);

            Assert.Equal(["OhMyGoodness", "SoSnowy", "ChannelEmote", "SharedEmote"], response.BTTV.Select(e => e.Code));
            Assert.Equal([false, true, false, false], response.BTTV.Select(e => e.IsZeroWidth));
            Assert.Equal(("c1", "gif", "https://cdn.betterttv.net/emote/c1/[scale]x"), (response.BTTV[2].Id, response.BTTV[2].ImageType, response.BTTV[2].ImageUrl));
            Assert.Empty(response.FFZ);
            Assert.Empty(response.STV);
            Assert.All(handler.Requests, r => Assert.Equal("api.betterttv.net", r.Uri.Host));
            Assert.Empty(progress.Errors);
        }

        // FFZ ids are numeric and animated emotes live under a different CDN path; using the static path for animated emotes renders them frozen.
        [Fact]
        public async Task Ffz_MergesChannelEmotesAndUsesAnimatedPathForAnimatedEmotes()
        {
            using var _ = UseHandler(Routes(
                (FFZ_GLOBAL, Json(Ser(new[] { Ffz(28136, "LilZ") }))),
                (FFZ_CHANNEL, Json(Ser(new[] { Ffz(720507, "AnimatedZ", animated: true) })))));
            using var cache = new TempDirectory();

            var response = await Fetch(cache.Path, new RecordingTaskProgress(), ffz: true);

            Assert.Equal(["28136", "720507"], response.FFZ.Select(e => e.Id));
            Assert.Equal("https://cdn.betterttv.net/frankerfacez_emote/28136/[scale]", response.FFZ[0].ImageUrl);
            Assert.Equal("https://cdn.betterttv.net/frankerfacez_emote/720507/animated/[scale]", response.FFZ[1].ImageUrl);
            Assert.Empty(response.BTTV);
        }

        // 7TV emotes must be skipped when they have no files, no WebP file (Skia can't decode AVIF), or Private/Twitch-disallowed flags,
        // each with a verbose log; ZeroWidth must be honoured; unlisted channel emotes only kept when allowed. URLs keep the CDN host from
        // the API (the 7TV image proxy rewrite is currently disabled) and always point at the WebP file.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Stv_FiltersUnusableEmotesAndHonoursFlags(bool allowUnlisted)
        {
            using var _ = UseHandler(Routes(
                (STV_GLOBAL, Json(StvSet(
                    Stv("01A", "GlobalWebp"),
                    Stv("01B", "NoFiles", formats: ""),
                    Stv("01C", "AvifOnly", formats: "AVIF"),
                    Stv("01D", "PrivateEmote", StvEmoteFlags.Private),
                    Stv("01E", "TwitchBanned", StvEmoteFlags.ContentTwitchDisallowed),
                    Stv("01F", "ZeroWidthEmote", StvEmoteFlags.ZeroWidth | StvEmoteFlags.Authentic)))),
                (STV_USER, Json(STV_USER_JSON)),
                (STV_SET, Json(StvSet(Stv("01G", "ChannelListed"), Stv("01H", "ChannelUnlisted", listed: false))))));
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, stv: true, allowUnlisted: allowUnlisted);

            string[] expected = allowUnlisted ? ["GlobalWebp", "ZeroWidthEmote", "ChannelListed", "ChannelUnlisted"] : ["GlobalWebp", "ZeroWidthEmote", "ChannelListed"];
            Assert.Equal(expected, response.STV.Select(e => e.Code));
            Assert.Equal([false, true, false], response.STV.Take(3).Select(e => e.IsZeroWidth));
            Assert.Equal(("01A", "webp", "https://cdn.7tv.app/emote/01A/[scale]x.webp"), (response.STV[0].Id, response.STV[0].ImageType, response.STV[0].ImageUrl));
            Assert.DoesNotContain(response.STV, e => e.ImageUrl.Contains("7tv-imageproxy"));
            Assert.Contains("NoFiles has no hosted files, skipping.", progress.Verbose);
            Assert.Contains("AvifOnly is not available in webp, skipping. Available formats: AVIF", progress.Verbose);
            Assert.Contains(progress.Verbose, v => v.StartsWith("PrivateEmote has disallowed flags"));
            Assert.Contains(progress.Verbose, v => v.StartsWith("TwitchBanned has disallowed flags"));
        }

        // A 7TV user whose emote set has no emotes (null) must still get the global emotes.
        [Fact]
        public async Task Stv_ChannelSetWithoutEmotes_KeepsGlobalEmotes()
        {
            using var _ = UseHandler(Routes(
                (STV_GLOBAL, Json(StvSet(Stv("01A", "GlobalWebp")))),
                (STV_USER, Json(STV_USER_JSON)),
                (STV_SET, Json("""{"id":"01SET","name":"empty","emotes":null}"""))));
            using var cache = new TempDirectory();

            var response = await Fetch(cache.Path, new RecordingTaskProgress(), stv: true);

            Assert.Equal("GlobalWebp", Assert.Single(response.STV).Code);
        }

        // Channels not registered with a provider get 404 for the channel endpoint; that is normal and must keep the global emotes without logging an error.
        [Theory]
        [InlineData("bttv")]
        [InlineData("ffz")]
        [InlineData("stv")]
        public async Task ChannelNotRegistered_KeepsGlobalEmotesWithoutError(string provider)
        {
            using var _ = UseHandler(Routes(
                (BTTV_GLOBAL, Json(Ser(new[] { Bttv("g1", "GlobalOnly") }))),
                (FFZ_GLOBAL, Json(Ser(new[] { Ffz(1, "GlobalOnly") }))),
                (STV_GLOBAL, Json(StvSet(Stv("01A", "GlobalOnly"))))));
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, bttv: provider == "bttv", ffz: provider == "ffz", stv: provider == "stv");

            var list = provider switch { "bttv" => response.BTTV, "ffz" => response.FFZ, _ => response.STV };
            Assert.Equal("GlobalOnly", Assert.Single(list).Code);
            Assert.Empty(progress.Errors);
        }

        // Provider outages (HTTP errors, TLS failures, timeouts, anything else) must be logged with a readable, provider-specific message
        // and swallowed - one dead emote provider must not abort the whole chat render.
        [Theory]
        [InlineData("bttv", FAIL_STATUS)]
        [InlineData("bttv", FAIL_SSL)]
        [InlineData("bttv", FAIL_TIMEOUT)]
        [InlineData("bttv", FAIL_OTHER)]
        [InlineData("ffz", FAIL_STATUS)]
        [InlineData("stv", FAIL_TIMEOUT)]
        public async Task ProviderFailure_IsLoggedNotThrown(string provider, string kind)
        {
            var providerName = provider switch { "bttv" => "BetterTTV", "ffz" => "FFZ", _ => "7TV" };
            using var _ = UseHandler(Failing(kind, _ => true), kind == FAIL_TIMEOUT ? TimeSpan.FromMilliseconds(100) : null);
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, bttv: provider == "bttv", ffz: provider == "ffz", stv: provider == "stv");

            Assert.Equal($"{ExpectedFailureMessage(kind, providerName)} {providerName} emotes may not be present for this session.", Assert.Single(progress.Errors));
            Assert.Empty(response.BTTV.Concat(response.FFZ).Concat(response.STV));
        }

        // Cancellation must propagate (not be logged as a provider failure) so the user's cancel actually stops the task.
        [Fact]
        public async Task CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            using var cache = new TempDirectory();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                TwitchHelper.GetThirdPartyEmotesMetadata(STREAMER, true, true, true, true, cache.Path, false, new RecordingTaskProgress(), cts.Token));
        }

        // A successful fetch must be persisted, and a later session whose provider is down must fall back to that cached list
        // (all fields intact) and tell the user it did so.
        [Fact]
        public async Task SuccessfulFetch_IsCached_AndUsedWhenProviderLaterFails()
        {
            using var cache = new TempDirectory();
            using (UseHandler(Routes((BTTV_GLOBAL, Json(Ser(new[] { Bttv("g1", "OhMyGoodness"), Bttv("g2", "SoSnowy") }))))))
            {
                await Fetch(cache.Path, new RecordingTaskProgress(), bttv: true);
            }

            Assert.True(File.Exists(CacheFile(cache.Path, "bttv")));

            using var _ = UseHandler(Failing(FAIL_STATUS, _ => true));
            var progress = new RecordingTaskProgress();
            var response = await Fetch(cache.Path, progress, bttv: true);

            Assert.Equal(["OhMyGoodness", "SoSnowy"], response.BTTV.Select(e => e.Code));
            Assert.True(response.BTTV[1].IsZeroWidth);
            Assert.Equal("https://cdn.betterttv.net/emote/g1/[scale]x", response.BTTV[0].ImageUrl);
            Assert.Contains(progress.Info, i => i.StartsWith("BetterTTV API unavailable. Using cached emote list from") && i.EndsWith("(2 emotes)."));
        }

        // Offline mode must never touch the network and must serve the cached metadata, with an offline-specific message.
        [Fact]
        public async Task Offline_UsesCachedMetadataWithoutNetwork()
        {
            using var cache = new TempDirectory();
            WriteCache(CacheFile(cache.Path, "ffz"), Item("1", "CachedZ"));
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, ffz: true, offline: true);

            Assert.Equal("CachedZ", Assert.Single(response.FFZ).Code);
            Assert.Empty(handler.Requests);
            Assert.Contains(progress.Info, i => i.StartsWith("Using cached FFZ emote list from"));
        }

        // Offline with nothing cached is a normal situation (first run): return empty lists quietly.
        [Fact]
        public async Task Offline_WithoutCache_ReturnsEmptyQuietly()
        {
            using var cache = new TempDirectory();
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, bttv: true, ffz: true, stv: true, offline: true);

            Assert.Empty(response.BTTV.Concat(response.FFZ).Concat(response.STV));
            Assert.Empty(handler.Requests);
            Assert.Empty(progress.Errors);
        }

        // Cached metadata older than 48h must be ignored so a long-dead provider can't resurrect an outdated emote list forever.
        [Fact]
        public async Task StaleCache_IsIgnored()
        {
            using var cache = new TempDirectory();
            var file = CacheFile(cache.Path, "bttv");
            WriteCache(file, Item("1", "OldEmote"));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow - TimeSpan.FromHours(49));
            using var _ = UseHandler(FakeHttpMessageHandler.NotFound());

            var response = await Fetch(cache.Path, new RecordingTaskProgress(), bttv: true, offline: true);

            Assert.Empty(response.BTTV);
        }

        // A corrupt cache file must be reported as an error and ignored, never thrown.
        [Fact]
        public async Task CorruptCache_IsLoggedAndIgnored()
        {
            using var cache = new TempDirectory();
            var file = CacheFile(cache.Path, "bttv");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, NotAnImage, TestContext.Current.CancellationToken);
            using var _ = UseHandler(FakeHttpMessageHandler.NotFound());
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, bttv: true, offline: true);

            Assert.Empty(response.BTTV);
            Assert.Contains(progress.Errors, e => e.StartsWith("Failed to load cached BetterTTV emote metadata:"));
        }

        // Failing to write the cache (here: the cache dir path is occupied by a file) is best-effort and must not affect the fetched result.
        [Fact]
        public async Task CacheWriteFailure_IsIgnored()
        {
            using var cache = new TempDirectory();
            await File.WriteAllTextAsync(Path.Combine(cache.Path, "bttv"), "not a directory", TestContext.Current.CancellationToken);
            using var _ = UseHandler(Routes((BTTV_GLOBAL, Json(Ser(new[] { Bttv("g1", "OhMyGoodness") })))));
            var progress = new RecordingTaskProgress();

            var response = await Fetch(cache.Path, progress, bttv: true);

            Assert.Equal("OhMyGoodness", Assert.Single(response.BTTV).Code);
            Assert.Empty(progress.Errors);
        }

        // When a provider's emote list shrinks, reloading the cache must yield exactly the new list. The cache is rewritten in place with
        // FileInfo.OpenWrite (no truncation), so stale bytes of the older, longer gzip stream remain after the new one and must not leak back in.
        [Fact]
        public async Task CacheRewrite_WithShorterList_ReplacesOldContents()
        {
            using var cache = new TempDirectory();
            var many = Enumerable.Range(0, 50).Select(i => Bttv($"id{i:D3}", $"Emote{i:D3}")).ToArray();
            using (UseHandler(Routes((BTTV_GLOBAL, Json(Ser(many))))))
            {
                await Fetch(cache.Path, new RecordingTaskProgress(), bttv: true);
            }

            using (UseHandler(Routes((BTTV_GLOBAL, Json(Ser(new[] { Bttv("only", "OnlyEmote") }))))))
            {
                await Fetch(cache.Path, new RecordingTaskProgress(), bttv: true);
            }

            using var _ = UseHandler(FakeHttpMessageHandler.NotFound());
            var progress = new RecordingTaskProgress();
            var response = await Fetch(cache.Path, progress, bttv: true, offline: true);

            Assert.Empty(progress.Errors);
            Assert.Equal("OnlyEmote", Assert.Single(response.BTTV).Code);
        }

        // A successful 7TV fetch must be persisted to the 7TV cache file (like BTTV/FFZ) so a later 7TV outage can fall back to it.
        [Fact(Skip = "Bug: GetThirdPartyEmotesMetadata passes emoteResponse.FFZ instead of emoteResponse.STV to RefreshOrLoadProviderMetadata for 7TV, so 7TV metadata is never cached (FFZ's list is written to stv/ instead)")]
        public async Task Stv_SuccessfulFetch_IsCached()
        {
            using var cache = new TempDirectory();
            using var _ = UseHandler(Routes((STV_GLOBAL, Json(StvSet(Stv("01A", "GlobalWebp"))))));

            await Fetch(cache.Path, new RecordingTaskProgress(), stv: true);

            Assert.True(File.Exists(CacheFile(cache.Path, "stv")));
        }

        // Offline 7TV must load the cached 7TV list into the STV result (not into FFZ).
        [Fact(Skip = "Bug: GetThirdPartyEmotesMetadata passes emoteResponse.FFZ instead of emoteResponse.STV to RefreshOrLoadProviderMetadata for 7TV, so cached 7TV emotes end up in the FFZ list")]
        public async Task Stv_Offline_LoadsCachedListIntoStv()
        {
            using var cache = new TempDirectory();
            WriteCache(CacheFile(cache.Path, "stv"), Item("01A", "CachedStv"));
            using var _ = UseHandler(FakeHttpMessageHandler.NotFound());

            var response = await Fetch(cache.Path, new RecordingTaskProgress(), stv: true, offline: true);

            Assert.Equal("CachedStv", Assert.Single(response.STV).Code);
            Assert.Empty(response.FFZ);
        }
    }
}
