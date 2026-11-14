using TwitchDownloaderCore.Models.Render;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    public class SectionImageCacheTests
    {
        [Theory]
        [InlineData(400, 20)]
        [InlineData(400, 350)]
        [InlineData(1, 1)]
        public void RentsAnImageOfTheRequestedSize(int width, int height)
        {
            using var cache = new SectionImageCache();

            var image = cache.Rent(width, height);

            Assert.Equal(width, image.Info.Width);
            Assert.Equal(height, image.Info.Height);
        }

        [Fact]
        public void ReusesAReturnedImageOfTheSameSize()
        {
            using var cache = new SectionImageCache();

            var first = cache.Rent(400, 20);
            var bitmap = first.Bitmap;
            cache.Return(first);

            var second = cache.Rent(400, 20);

            Assert.Same(bitmap, second.Bitmap);
        }

        [Fact]
        public void DoesNotReuseAnImageOfADifferentHeight()
        {
            using var cache = new SectionImageCache();

            // A chat GIF takes a section of its own height, so sections are not all the same size
            var section = cache.Rent(400, 20);
            var bitmap = section.Bitmap;
            cache.Return(section);

            var gifSection = cache.Rent(400, 350);

            Assert.NotSame(bitmap, gifSection.Bitmap);
            Assert.Equal(350, gifSection.Info.Height);
        }

        [Fact]
        public void KeepsEachHeightInItsOwnBucket()
        {
            using var cache = new SectionImageCache();

            var small = cache.Rent(400, 20);
            var large = cache.Rent(400, 350);
            var smallBitmap = small.Bitmap;
            var largeBitmap = large.Bitmap;
            cache.Return(small);
            cache.Return(large);

            // Returning both and renting them back in the opposite order must not cross the buckets
            Assert.Same(largeBitmap, cache.Rent(400, 350).Bitmap);
            Assert.Same(smallBitmap, cache.Rent(400, 20).Bitmap);
        }

        [Fact]
        public void ClearsAReusedImageBeforeHandingItBack()
        {
            using var cache = new SectionImageCache();

            var first = cache.Rent(4, 4);
            first.Canvas.Clear(SkiaSharp.SKColors.Red);
            first.Flush();
            Assert.Equal(SkiaSharp.SKColors.Red, first.Bitmap.GetPixel(0, 0));
            cache.Return(first);

            var second = cache.Rent(4, 4);
            second.Flush();

            Assert.Equal(default, second.Bitmap.GetPixel(0, 0));
        }

        [Fact]
        public void DisposesAnImageItNeverRentedOut()
        {
            using var cache = new SectionImageCache();

            var foreign = new SectionImage(400, 20);
            cache.Return(foreign);

            // It was disposed rather than pooled, so the next rent cannot be the same image
            Assert.NotSame(foreign.Bitmap, cache.Rent(400, 20).Bitmap);
        }

        [Fact]
        public void SurvivesMoreDistinctHeightsThanItHasBuckets()
        {
            using var cache = new SectionImageCache();

            // Every chat GIF has its own height, so a busy VOD asks for far more sizes than the bucket limit
            for (var height = 1; height <= 120; height++)
            {
                var image = cache.Rent(400, height);
                Assert.Equal(height, image.Info.Height);
                cache.Return(image);
            }
        }
    }
}
