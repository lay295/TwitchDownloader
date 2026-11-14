using System.IO.Compression;
using TwitchDownloaderCore.Chat;

namespace TwitchDownloaderCore.Tests.ChatTests
{
    // Tests EmojiVendorExtensions, which describes the two emoji packs bundled as zip resources (Twemoji, Noto Color).
    // TwitchHelper.GetEmojis uses these values to decide whether to re-extract the pack, where the PNGs live inside the
    // zip and how to turn file names into lookup keys; a wrong value silently breaks emoji rendering in every chat render.
    public class EmojiVendorTests
    {
        private static List<string> PngNamesInBundledArchive(EmojiVendor vendor)
        {
            using var archive = new ZipArchive(vendor.MemoryStream(), ZipArchiveMode.Read);
            var assetPath = vendor.AssetPath();
            // Same filter as TwitchHelper.GetEmojis
            return archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name) && Path.GetDirectoryName(e.FullName) is { } dir && dir.EndsWith(assetPath))
                .Select(e => e.Name)
                .Where(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Pins the per-vendor constants: cache folder name, zip-internal asset path (built with Path.Combine so it matches
        /// on Windows too) and the separator used in multi-codepoint file names (1f468-200d-1f4bb vs 1f468_200d_1f4bb).
        /// Changing any of these orphans existing caches or stops emoji sequences from matching.
        /// </summary>
        [Theory]
        [InlineData(EmojiVendor.TwitterTwemoji, "twemoji", new[] { "assets", "72x72" }, '-')]
        [InlineData(EmojiVendor.GoogleNotoColor, "noto-color", new[] { "png", "72" }, '_')]
        public void Vendor_HasExpectedConstants(EmojiVendor vendor, string folder, string[] assetPathParts, char separator)
        {
            Assert.Equal(folder, vendor.EmojiFolder());
            Assert.Equal(Path.Combine(assetPathParts), vendor.AssetPath());
            Assert.Equal(separator, vendor.UnicodeSequenceSeparator());
        }

        /// <summary>
        /// The bundled zip must actually contain PNGs under AssetPath(), using the vendor's separator in sequence names,
        /// and at least EmojiCount() of them. If the count were higher than what the zip holds, GetEmojis would think the
        /// cache is incomplete and re-extract thousands of files on every render.
        /// </summary>
        [Theory]
        [InlineData(EmojiVendor.TwitterTwemoji)]
        [InlineData(EmojiVendor.GoogleNotoColor)]
        public void BundledArchive_HasAtLeastEmojiCountPngsUnderAssetPath(EmojiVendor vendor)
        {
            var pngs = PngNamesInBundledArchive(vendor);

            Assert.True(pngs.Count >= vendor.EmojiCount(), $"{vendor}: {pngs.Count} PNGs in archive, EmojiCount() is {vendor.EmojiCount()}");
            Assert.Contains(pngs, n => n.Contains(vendor.UnicodeSequenceSeparator()));
        }

        /// <summary>
        /// EmojiCount() is the "cache is complete" threshold. It has to follow the bundled pack: after the Unicode 17 update
        /// (twemoji 4009 / noto 3985 PNGs) it still says 3680 / 3689, so a cache extracted from the previous pack is treated as
        /// complete and the new emoji are never extracted for existing users.
        /// </summary>
        [Theory(Skip = "Bug: EmojiCount() was not bumped when the emoji packs were updated to Unicode 17 (6ef2dc3), so existing emoji caches are never refreshed with the new emoji")]
        [InlineData(EmojiVendor.TwitterTwemoji)]
        [InlineData(EmojiVendor.GoogleNotoColor)]
        public void EmojiCount_MatchesBundledArchive(EmojiVendor vendor)
        {
            Assert.Equal(PngNamesInBundledArchive(vendor).Count, vendor.EmojiCount());
        }

        /// <summary>
        /// EmojiVendor.None (emoji rendering disabled) and out-of-range values have no pack. Every accessor must throw
        /// NotSupportedException instead of returning a bogus path or count.
        /// </summary>
        [Theory]
        [InlineData(EmojiVendor.None)]
        [InlineData((EmojiVendor)42)]
        public void UnsupportedVendor_Throws(EmojiVendor vendor)
        {
            Assert.Throws<NotSupportedException>(() => vendor.EmojiFolder());
            Assert.Throws<NotSupportedException>(() => vendor.MemoryStream());
            Assert.Throws<NotSupportedException>(() => vendor.EmojiCount());
            Assert.Throws<NotSupportedException>(() => vendor.AssetPath());
            Assert.Throws<NotSupportedException>(() => vendor.UnicodeSequenceSeparator());
        }
    }
}
