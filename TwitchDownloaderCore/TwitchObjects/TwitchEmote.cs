using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects
{
    
    public enum EmoteProvider
    {
        FirstParty,
        ThirdParty
    }
    public class TwitchEmote
    {
        public SKCodec Codec { get; set; }
        public byte[] ImageData { get; set; }
        public EmoteProvider EmoteProvider { get; set; }
        public List<SKBitmap> EmoteFrames { get; set; } = new List<SKBitmap>();
        public List<int> EmoteFrameDurations { get; set; } = new List<int>();
        public int TotalDuration { get; set; }
        public string Name { get; set; }
        public string Id { get; set; }
        public int ImageScale { get; set; }
        public bool IsZeroWidth { get; set; } = false;
        public int FrameCount
        {
            get
            {
                if (Codec.FrameCount == 0)
                    return 1;
                else
                    return Codec.FrameCount;
            }
        }
        public int Height { get { return EmoteFrames[0].Height; } }
        public int Width { get { return EmoteFrames[0].Width; } }

        public TwitchEmote(byte[] imageData, EmoteProvider emoteProvider, int imageScale, string imageId, string imageName)
        {
            using MemoryStream ms = new MemoryStream(imageData);
            Codec = SKCodec.Create(ms);
            EmoteProvider = emoteProvider;
            Id = imageId;
            Name = imageName;
            ImageScale = imageScale;
            ImageData = imageData;

            ExtractFrames();
            CalculateDurations();
        }

        private void CalculateDurations()
        {
            EmoteFrameDurations = new List<int>();
            for (int i = 0; i < Codec.FrameCount; i++)
            {
                var duration = Codec.FrameInfo[i].Duration / 10;
                EmoteFrameDurations.Add(duration);
                TotalDuration += duration;
            }

            if (TotalDuration == 0 || TotalDuration == Codec.FrameCount)
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
            for (int i = 0; i < FrameCount; i++)
            {
                SKImageInfo imageInfo = new SKImageInfo(Codec.Info.Width, Codec.Info.Height);
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                IntPtr pointer = newBitmap.GetPixels();
                SKCodecOptions codecOptions = new SKCodecOptions(i);
                Codec.GetPixels(imageInfo, pointer, codecOptions);
                EmoteFrames.Add(newBitmap);
            }
        }

        public void Resize(double newScale)
        {
            for (int i = 0; i < FrameCount; i++)
            {
                SKImageInfo imageInfo = new SKImageInfo((int)(Codec.Info.Width * newScale), (int)(Codec.Info.Height * newScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                EmoteFrames[i].ScalePixels(newBitmap, SKFilterQuality.High);
                EmoteFrames[i].Dispose();
                EmoteFrames[i] = newBitmap;
            }
        }
    }
}
