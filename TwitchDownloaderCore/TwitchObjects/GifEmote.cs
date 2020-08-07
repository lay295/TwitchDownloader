using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class GifEmote
    {
        public Point Offset { get; set; }
        public string Name { get; set; }
        public SKCodec Codec { get; set; }
        public int FrameCount { get; set; }
        public List<int> DurationList { get; set; }
        public int TotalDuration { get; set; }
        public int ImageScale { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<SKBitmap> ImageFrames { get; set; }
        public SKBitmap BackgroundImage { get; set; }

        public GifEmote(Point offset, string name, SKCodec codec, int imageScale, List<SKBitmap> imageFrames)
        {
            Offset = offset;
            Name = name;
            Codec = codec;
            ImageScale = imageScale;
            ImageFrames = imageFrames;
            FrameCount = codec.FrameCount;

            DurationList = new List<int>();
            for (int i = 0; i < FrameCount; i++)
            {
                var duration = Codec.FrameInfo[i].Duration / 10;
                DurationList.Add(duration);
                TotalDuration += duration;
            }

            if (TotalDuration == 0 || TotalDuration == FrameCount)
            {
                for (int i = 0; i < DurationList.Count; i++)
                {
                    DurationList.RemoveAt(i);
                    DurationList.Insert(i, 10);
                }
                TotalDuration = DurationList.Count * 10;
            }

            for (int i = 0; i < DurationList.Count; i++)
            {
                if (DurationList[i] == 0)
                {
                    TotalDuration += 10;
                    DurationList[i] = 10;
                }
            }

            Width = ImageFrames.First().Width;
            Height = ImageFrames.First().Height;

            imageScale = ImageScale;
        }
    }
}
