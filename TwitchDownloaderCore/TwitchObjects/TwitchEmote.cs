using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TwitchDownloaderCore.TwitchObjects
{
    public enum EmoteProvider
    {
        FirstParty,
        ThirdParty
    }

    [DebuggerDisplay("{Name}")]
    public sealed class TwitchEmote : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public SKCodec Codec { get; }
        public byte[] ImageData { get; set; }
        public EmoteProvider EmoteProvider { get; set; }
        public List<SKBitmap> EmoteFrames { get; } = new List<SKBitmap>();
        public List<int> EmoteFrameDurations { get; private set; } = new List<int>();
        public int TotalDuration { get; set; }
        public string Name { get; }
        public string Id { get; }
        public int ImageScale { get; }
        public bool IsZeroWidth { get; set; } = false;
        public int FrameCount { get; }
        public int Height => EmoteFrames[0].Height;
        public int Width => EmoteFrames[0].Width;
        public SKImageInfo Info => EmoteFrames[0].Info;

        public TwitchEmote(byte[] imageData, EmoteProvider emoteProvider, int imageScale, string imageId, string imageName)
        {
            using MemoryStream ms = new MemoryStream(imageData);
            Codec = SKCodec.Create(ms, out var result);
            if (Codec is null)
                throw new Exception($"Skia was unable to decode {imageName} ({imageId}). Returned: {result}");

            EmoteProvider = emoteProvider;
            Id = imageId;
            Name = imageName;
            ImageScale = imageScale;
            ImageData = imageData;
            FrameCount = Math.Max(1, Codec.FrameCount);

            ExtractFrames();
            CalculateDurations();
        }

        private void CalculateDurations()
        {
            EmoteFrameDurations = new List<int>(FrameCount);

            if (FrameCount == 1)
                return;

            var frameInfos = Codec.FrameInfo;
            for (int i = 0; i < FrameCount; i++)
            {
                var duration = frameInfos[i].Duration / 10;
                EmoteFrameDurations.Add(duration);
                TotalDuration += duration;
            }

            if (TotalDuration == 0 || TotalDuration == FrameCount)
            {
                for (int i = 0; i < EmoteFrameDurations.Count; i++)
                {
                    EmoteFrameDurations.RemoveAt(i);
                    EmoteFrameDurations.Insert(i, 10);
                }
                TotalDuration = EmoteFrameDurations.Count * 10;
            }

            for (var i = 0; i < EmoteFrameDurations.Count; ++i) {
                if (this.EmoteFrameDurations[i] != 0)
                    continue;

                this.TotalDuration += 10;
                this.EmoteFrameDurations[i] = 10;
            }
        }

        private void ExtractFrames()
        {
            var codecInfo = Codec.Info;
            for (var i = 0; i < FrameCount; ++i)
            {
                var imageInfo = new SKImageInfo(codecInfo.Width, codecInfo.Height);
                var newBitmap = new SKBitmap(imageInfo);
                var pointer = newBitmap.GetPixels();
                var codecOptions = new SKCodecOptions(i);
                this.Codec.GetPixels(imageInfo, pointer, codecOptions);
                newBitmap.SetImmutable();
                this.EmoteFrames.Add(newBitmap);
            }
        }

        public void Resize(double newScale)
        {
            var codecInfo = this.Codec.Info;
            for (var i = 0; i < this.FrameCount; ++i)
            {
                var imageInfo = new SKImageInfo((int)(codecInfo.Width * newScale), (int)(codecInfo.Height * newScale));
                var newBitmap = new SKBitmap(imageInfo);
                this.EmoteFrames[i].ScalePixels(newBitmap, SKFilterQuality.High);
                this.EmoteFrames[i].Dispose();
                newBitmap.SetImmutable();
                this.EmoteFrames[i] = newBitmap;
            }
        }

#region ImplementIDisposable

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (Disposed)
                {
                    return;
                }

                if (isDisposing)
                {
                    foreach (var bitmap in EmoteFrames)
                    {
                        bitmap?.Dispose();
                    }

                    Codec?.Dispose();
                }
            }
            finally
            {
                Disposed = true;
            }
        }

#endregion
    }
}
