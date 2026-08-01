using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TwitchDownloaderCore.Models.Render
{
    public sealed class SectionImageCache : IDisposable
    {
        private const int MAX_BUCKETS = 50;
        private const int MIN_BUCKET_SIZE = 3;
        private const int BUCKET_MAX_BYTE_SIZE = 20_000_000;

        private readonly Dictionary<(int, int), List<SectionImage>> _sectionImageCache = [];

        public SectionImage Rent(int width, int height)
        {
            if (_sectionImageCache.Count > MAX_BUCKETS)
            {
                // Too many buckets, delete a random bucket to save memory
                var bucketId = Random.Shared.Next(0, _sectionImageCache.Count);
                var (key, val) = _sectionImageCache.Skip(bucketId).FirstOrDefault();

                _sectionImageCache.Remove(key);
                foreach (var i in val) i.Dispose();
            }

            ref var bucket = ref CollectionsMarshal.GetValueRefOrAddDefault(_sectionImageCache, (width, height), out _);
            bucket ??= [];

            if (bucket.Count == 0)
            {
                return new SectionImage(width, height);
            }

            var image = bucket[^1];
            bucket.RemoveAt(bucket.Count - 1);
            image.Canvas.Clear();
            return image;
        }

        public void Return(SectionImage sectionImage)
        {
            var width = sectionImage.Info.Width;
            var height = sectionImage.Info.Height;

            ref var bucket = ref CollectionsMarshal.GetValueRefOrNullRef(_sectionImageCache, (width, height));
            if (Unsafe.IsNullRef(ref bucket))
            {
                // Don't create a new bucket for an image that wasn't rented from the cache
                sectionImage.Dispose();
                return;
            }

            var bytesPerImage = sectionImage.Info.BytesSize;
            if (bucket.Count < MIN_BUCKET_SIZE || bucket.Count * bytesPerImage < BUCKET_MAX_BYTE_SIZE)
            {
                bucket.Add(sectionImage);
            }
            else
            {
                sectionImage.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var (_, bucket) in _sectionImageCache)
            {
                foreach (var image in bucket)
                {
                    image.Dispose();
                }
            }
        }
    }
}