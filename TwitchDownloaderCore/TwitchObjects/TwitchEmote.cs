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
                throw new BadImageFormatException($"Skia was unable to decode {imageName} ({imageId}). Returned: {result}");

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

            for (int i = 0; i < EmoteFrameDurations.Count; i++)
            {
                if (EmoteFrameDurations[i] == 0)
                {
                    TotalDuration += 10;
                    EmoteFrameDurations[i] = 10;
                }
            }
        }

        private void ExtractFrames()
        {
            var codecInfo = Codec.Info;
            for (int i = 0; i < FrameCount; i++)
            {
                SKImageInfo imageInfo = new SKImageInfo(codecInfo.Width, codecInfo.Height);
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                IntPtr pointer = newBitmap.GetPixels();
                SKCodecOptions codecOptions = new SKCodecOptions(i);
                Codec.GetPixels(imageInfo, pointer, codecOptions);
                newBitmap.SetImmutable();
                EmoteFrames.Add(newBitmap);
            }
        }

        public void Resize(double newScale)
        {
            var codecInfo = Codec.Info;
            for (int i = 0; i < FrameCount; i++)
            {
                SKImageInfo imageInfo = new SKImageInfo((int)(codecInfo.Width * newScale), (int)(codecInfo.Height * newScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                EmoteFrames[i].ScalePixels(newBitmap, SKFilterQuality.High);
                EmoteFrames[i].Dispose();
                newBitmap.SetImmutable();
                EmoteFrames[i] = newBitmap;
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
