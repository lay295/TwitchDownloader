using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Tests.TestHelpers;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper.GetEmojis, which unpacks the emoji set embedded in the Core assembly (Twemoji / Noto zips) into the
    // cache folder and decodes it for the chat renderer. The keys must match the renderer's codepoint lookup, and the cache must be
    // reused (not re-extracted) once complete. Uses the real embedded zips; no network is involved.
    public class TwitchHelperEmojiTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        // EmojiVendor.None disables emojis: nothing may be extracted or even created on disk.
        [Fact]
        public async Task None_ReturnsEmptyWithoutTouchingDisk()
        {
            using var cache = new TempDirectory();

            var emojis = await TwitchHelper.GetEmojis(cache.Path, EmojiVendor.None, new RecordingTaskProgress(), Ct);

            Assert.Empty(emojis);
            Assert.Empty(Directory.EnumerateFileSystemEntries(cache.Path));
        }

        // An unknown vendor value must fail loudly instead of silently rendering without emojis.
        [Fact]
        public async Task UnknownVendor_Throws()
        {
            using var cache = new TempDirectory();

            await Assert.ThrowsAsync<NotSupportedException>(() => TwitchHelper.GetEmojis(cache.Path, (EmojiVendor)99, new RecordingTaskProgress(), Ct));
        }

        // First run extracts every Twemoji PNG to emojis/twemoji with upper-case, space-separated codepoint names (the renderer's lookup key),
        // leaves no temp zip behind, and yields at least EmojiCount images (fewer would trigger a re-extract on every render).
        // A second run must reuse the cache: an extra undecodable file is reported as a warning rather than aborting.
        [Fact]
        public async Task Twemoji_ExtractsOnceThenReusesCache()
        {
            using var cache = new TempDirectory();
            var folder = cache.Combine("emojis", "twemoji");

            var first = await TwitchHelper.GetEmojis(cache.Path, EmojiVendor.TwitterTwemoji, new RecordingTaskProgress(), Ct);

            Assert.True(first.Count >= EmojiVendor.TwitterTwemoji.EmojiCount(), $"Only {first.Count} emojis");
            Assert.Equal(72, first["1F600"].Width);
            Assert.True(first.ContainsKey("1F344 200D 1F7EB"));
            Assert.All(Directory.GetFiles(folder), f => Assert.Equal(".PNG", Path.GetExtension(f))); // The whole file name is upper-cased
            foreach (var bitmap in first.Values) bitmap.Dispose();

            await File.WriteAllBytesAsync(Path.Combine(folder, "ZZZZ.png"), NotAnImage, Ct);
            var progress = new RecordingTaskProgress();
            var second = await TwitchHelper.GetEmojis(cache.Path, EmojiVendor.TwitterTwemoji, progress, Ct);

            Assert.Equal(first.Count, second.Count);
            Assert.Contains("Failed to decode emoji ZZZZ.png, skipping.", progress.Verbose);
            Assert.Contains("1 emojis failed to decode.", progress.Warnings);
            foreach (var bitmap in second.Values) bitmap.Dispose();
        }

        // Noto uses '_' as its sequence separator and a different archive path; keys must still come out as "1F3C3 1F3FB 200D 2640".
        // Files already present in an incomplete cache must not be overwritten (another process may be writing them), and an emoji that
        // can't be extracted (here: a directory blocks its path) must be skipped silently instead of aborting the render.
        [Fact]
        public async Task NotoColor_ExtractsWithItsOwnLayoutAndKeepsExistingFiles()
        {
            using var cache = new TempDirectory();
            var folder = cache.Combine("emojis", "noto-color");
            Directory.CreateDirectory(folder);
            var existing = Path.Combine(folder, "1F600.PNG"); // Same naming as the extractor (left by an interrupted or parallel run)
            var smiley = Png(10, 10);
            await File.WriteAllBytesAsync(existing, smiley, Ct);
            Directory.CreateDirectory(Path.Combine(folder, "1F601.PNG"));

            var emojis = await TwitchHelper.GetEmojis(cache.Path, EmojiVendor.GoogleNotoColor, new RecordingTaskProgress(), Ct);

            Assert.True(emojis.Count >= EmojiVendor.GoogleNotoColor.EmojiCount(), $"Only {emojis.Count} emojis");
            Assert.True(emojis.ContainsKey("1F3C3 1F3FB 200D 2640"));
            Assert.Equal(10, emojis["1F600"].Width);
            Assert.Equal(smiley, await File.ReadAllBytesAsync(existing, Ct));
            Assert.False(emojis.ContainsKey("1F601"));
            foreach (var bitmap in emojis.Values) bitmap.Dispose();
        }
    }
}
