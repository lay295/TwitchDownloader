using SkiaSharp;

namespace TwitchDownloaderCore.Extensions
{
    public static class SKBitmapExtensions
    {
        /// <summary>
        /// Pixel bytes for <paramref name="bitmap"/>.
        /// Do not use <see cref="SKBitmap.GetPixelSpan"/> from this project: SkiaSharp 2 returns
        /// <see cref="ReadOnlySpan{T}"/> while SkiaSharp 3 returns <see cref="Span{T}"/>, so a Core
        /// binary compiled against 2.x throws <see cref="MissingMethodException"/> when Avalonia 12
        /// loads SkiaSharp 3 at runtime. <see cref="SKBitmap.GetPixels()"/> is stable across both.
        /// </summary>
        public static unsafe ReadOnlySpan<byte> GetPixelBytes(this SKBitmap bitmap)
        {
            var ptr = bitmap.GetPixels();
            if (ptr == nint.Zero || bitmap.ByteCount <= 0)
                return ReadOnlySpan<byte>.Empty;

            return new ReadOnlySpan<byte>(ptr.ToPointer(), bitmap.ByteCount);
        }
    }
}
