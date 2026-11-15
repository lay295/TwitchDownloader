using SkiaSharp;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    /// <summary>Tiny images for the SkiaSharp-backed emote/badge classes.</summary>
    internal static class TestImages
    {
        /// <summary>A solid-colour PNG of the given size, encoded by Skia so it is always decodable by Skia.</summary>
        public static byte[] Png(int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(SKColors.CornflowerBlue);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <summary>
        /// One of the committed 8x6, 3-frame GIFs under TestData/Chat whose per-frame delays (in 1/100 s) are in the name,
        /// e.g. anim_delays_2_0_5.gif.
        /// </summary>
        public static byte[] Gif(string name) => File.ReadAllBytes(TestData.Path("Chat", name));
    }
}
