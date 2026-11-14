using SkiaSharp;
using TwitchDownloaderCore.Models.Render;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    // Tests for SectionImageCache, the pool that recycles the bitmaps every chat line is drawn on. A pooling bug either leaks memory
    // over a multi-hour render or hands out a bitmap that still contains (or has disposed) another line's pixels.
    public class SectionImageCacheTests
    {
        private static bool IsDisposed(SectionImage image) => image.Bitmap.Handle == IntPtr.Zero;

        /// <summary>
        /// A returned image is handed out again for the same size, cleared of whatever was drawn on it. Guards against ghost text from a
        /// previous comment appearing in a new one.
        /// </summary>
        [Fact]
        public void Rent_AfterReturn_ReusesClearedImage()
        {
            using var cache = new SectionImageCache();
            var first = cache.Rent(10, 5);
            first.Canvas.Clear(SKColors.Red);
            first.Flush();
            cache.Return(first);

            var second = cache.Rent(10, 5);

            Assert.Same(first.Bitmap, second.Bitmap);
            Assert.Equal(0, second.Bitmap.GetPixel(3, 3).Alpha);
            cache.Return(second);
        }

        /// <summary>Different sizes live in different buckets, so a rented image always has the requested size.</summary>
        [Fact]
        public void Rent_DifferentSize_CreatesNewImageOfThatSize()
        {
            using var cache = new SectionImageCache();
            cache.Return(cache.Rent(10, 5));

            var other = cache.Rent(20, 7);

            Assert.Equal(20, other.Info.Width);
            Assert.Equal(7, other.Info.Height);
            cache.Return(other);
        }

        /// <summary>An image that was never rented (no bucket for its size) is disposed rather than starting a new bucket.</summary>
        [Fact]
        public void Return_ForeignImage_DisposesIt()
        {
            using var cache = new SectionImageCache();
            var foreign = new SectionImage(4, 4);

            cache.Return(foreign);

            Assert.True(IsDisposed(foreign));
        }

        /// <summary>Small images are all kept for reuse, so steady-state rendering allocates nothing.</summary>
        [Fact]
        public void Return_SmallImages_AreAllPooled()
        {
            using var cache = new SectionImageCache();
            var rented = Enumerable.Range(0, 5).Select(_ => cache.Rent(10, 5)).ToList();
            rented.ForEach(cache.Return);

            var again = Enumerable.Range(0, 5).Select(_ => cache.Rent(10, 5)).ToList();

            Assert.Equal(rented.Select(x => x.Bitmap).ToHashSet(), again.Select(x => x.Bitmap).ToHashSet());
            Assert.All(rented, x => Assert.False(IsDisposed(x)));
            again.ForEach(cache.Return);
        }

        /// <summary>
        /// A bucket of large images keeps at least three but stops growing past ~20 MB; extra returns are disposed.
        /// Guards the memory ceiling for tall comment images.
        /// </summary>
        [Fact]
        public void Return_LargeImages_CapsBucketSize()
        {
            using var cache = new SectionImageCache();
            var rented = Enumerable.Range(0, 4).Select(_ => cache.Rent(1300, 1300)).ToList(); // ~6.8 MB each

            rented.ForEach(cache.Return);

            Assert.Equal([false, false, false, true], rented.Select(IsDisposed));
        }

        /// <summary>
        /// Once more than 50 sizes are cached, renting a new size evicts (and disposes) exactly one existing bucket.
        /// Guards against unbounded growth when many differently sized comments are rendered.
        /// </summary>
        [Fact]
        public void Rent_TooManyBuckets_EvictsOneBucket()
        {
            using var cache = new SectionImageCache();
            var images = Enumerable.Range(1, 51).Select(h => cache.Rent(2, h)).ToList();
            images.ForEach(cache.Return);

            cache.Return(cache.Rent(2, 100));

            Assert.Equal(1, images.Count(IsDisposed));
        }

        /// <summary>Disposing the cache disposes every pooled image.</summary>
        [Fact]
        public void Dispose_DisposesPooledImages()
        {
            var cache = new SectionImageCache();
            var image = cache.Rent(10, 5);
            cache.Return(image);

            cache.Dispose();

            Assert.True(IsDisposed(image));
        }
    }

    // Tests for SectionImage, the bitmap + canvas pair a chat line is drawn on.
    public class SectionImageTests
    {
        /// <summary>The canvas draws onto the image's own bitmap, whose info matches the requested size.</summary>
        [Fact]
        public void Canvas_DrawsOntoBitmap()
        {
            using var image = new SectionImage(8, 6);

            image.Canvas.Clear(SKColors.Blue);
            image.Flush();

            Assert.Equal(8, image.Info.Width);
            Assert.Equal(6, image.Info.Height);
            Assert.Equal(SKColors.Blue, image.Bitmap.GetPixel(7, 5));
        }

        /// <summary>Dispose releases both the canvas and the bitmap so pooled images never leak native memory.</summary>
        [Fact]
        public void Dispose_ReleasesCanvasAndBitmap()
        {
            var image = new SectionImage(8, 6);

            image.Dispose();

            Assert.Equal(IntPtr.Zero, image.Bitmap.Handle);
            Assert.Equal(IntPtr.Zero, image.Canvas.Handle);
        }
    }
}
