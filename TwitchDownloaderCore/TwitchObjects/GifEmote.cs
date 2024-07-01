using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace TwitchDownloaderCore.TwitchObjects;

public class GifEmote {

    public GifEmote(Point offset, string name, SKCodec codec, int imageScale, List<SKBitmap> imageFrames) {
        this.Offset = offset;
        this.Name = name;
        this.Codec = codec;
        this.ImageScale = imageScale;
        this.ImageFrames = imageFrames;
        this.FrameCount = codec.FrameCount;

        this.DurationList = new();
        for (var i = 0; i < this.FrameCount; ++i) {
            var duration = this.Codec.FrameInfo[i].Duration / 10;
            this.DurationList.Add(duration);
            this.TotalDuration += duration;
        }

        if (this.TotalDuration == 0 || this.TotalDuration == this.FrameCount) {
            for (var i = 0; i < this.DurationList.Count; ++i) {
                this.DurationList.RemoveAt(i);
                this.DurationList.Insert(i, 10);
            }

            this.TotalDuration = this.DurationList.Count * 10;
        }

        for (var i = 0; i < this.DurationList.Count; ++i) {
            if (this.DurationList[i] != 0)
                continue;

            this.TotalDuration += 10;
            this.DurationList[i] = 10;
        }

        this.Width = this.ImageFrames.First().Width;
        this.Height = this.ImageFrames.First().Height;

        imageScale = this.ImageScale;
    }

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
}
