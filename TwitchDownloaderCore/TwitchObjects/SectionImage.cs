using SkiaSharp;

namespace TwitchDownloaderCore.TwitchObjects
{
    public readonly record struct SectionImage : IDisposable
    {
        public readonly SKImageInfo Info;
        public readonly SKBitmap Bitmap;
        public readonly SKCanvas Canvas;

        public SectionImage(int width, int height)
        {
            Bitmap = new SKBitmap(width, height);
            Info = Bitmap.Info;
            Canvas = new SKCanvas(Bitmap);
        }

        public void SetImmutable()
        {
            Canvas.Flush();
            Bitmap.SetImmutable();
        }

        public void Dispose()
        {
            Canvas.Dispose();
            Bitmap.Dispose();
        }
    }
}