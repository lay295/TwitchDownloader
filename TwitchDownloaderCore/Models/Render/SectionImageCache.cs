using System.Runtime.InteropServices;

namespace TwitchDownloaderCore.Models.Render
{
    public sealed class SectionImageCache : IDisposable
    {
        private readonly Dictionary<(int, int), List<SectionImage>> _sectionImageCache = [];

        public SectionImage Rent(int width, int height)
        {
            ref var bucket = ref CollectionsMarshal.GetValueRefOrAddDefault(_sectionImageCache, (width, height), out var exists);
            if (!exists)
            {
                bucket = [];
            }

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

            ref var bucket = ref CollectionsMarshal.GetValueRefOrAddDefault(_sectionImageCache, (width, height), out var exists);
            if (!exists)
            {
                // Don't create a new bucket for an image that wasn't rented from the cache
                sectionImage.Dispose();
                return;
            }

            bucket.Add(sectionImage);
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