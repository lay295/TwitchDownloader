using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SkiaSharp;

namespace TwitchDownloaderCore.TwitchObjects;

public enum EmoteProvider {
    FirstParty,
    ThirdParty
}

[DebuggerDisplay("{Name}")]
public sealed class TwitchEmote : IDisposable {

    public TwitchEmote(
        byte[] imageData,
        EmoteProvider emoteProvider,
        int imageScale,
        string imageId,
        string imageName
    ) {
        using var ms = new MemoryStream(imageData);
        this.Codec = SKCodec.Create(ms, out var result);
        if (this.Codec is null)
            throw new($"Skia was unable to decode {imageName} ({imageId}). Returned: {result}");

        this.EmoteProvider = emoteProvider;
        this.Id = imageId;
        this.Name = imageName;
        this.ImageScale = imageScale;
        this.ImageData = imageData;
        this.FrameCount = Math.Max(1, this.Codec.FrameCount);

        this.ExtractFrames();
        this.CalculateDurations();
    }

    public bool Disposed { get; private set; }
    public SKCodec Codec { get; }
    public byte[] ImageData { get; set; }
    public EmoteProvider EmoteProvider { get; set; }
    public List<SKBitmap> EmoteFrames { get; } = new();
    public List<int> EmoteFrameDurations { get; private set; } = new();
    public int TotalDuration { get; set; }
    public string Name { get; }
    public string Id { get; }
    public int ImageScale { get; }
    public bool IsZeroWidth { get; set; } = false;
    public int FrameCount { get; }
    public int Height => this.EmoteFrames[0].Height;
    public int Width => this.EmoteFrames[0].Width;
    public SKImageInfo Info => this.EmoteFrames[0].Info;

    private void CalculateDurations() {
        this.EmoteFrameDurations = new(this.FrameCount);

        if (this.FrameCount == 1)
            return;

        var frameInfos = this.Codec.FrameInfo;
        for (var i = 0; i < this.FrameCount; i++) {
            var duration = frameInfos[i].Duration / 10;
            this.EmoteFrameDurations.Add(duration);
            this.TotalDuration += duration;
        }

        if (this.TotalDuration == 0 || this.TotalDuration == this.FrameCount) {
            for (var i = 0; i < this.EmoteFrameDurations.Count; i++) {
                this.EmoteFrameDurations.RemoveAt(i);
                this.EmoteFrameDurations.Insert(i, 10);
            }

            this.TotalDuration = this.EmoteFrameDurations.Count * 10;
        }

        for (var i = 0; i < this.EmoteFrameDurations.Count; ++i) {
            if (this.EmoteFrameDurations[i] != 0)
                continue;

            this.TotalDuration += 10;
            this.EmoteFrameDurations[i] = 10;
        }
    }

    private void ExtractFrames() {
        var codecInfo = this.Codec.Info;
        for (var i = 0; i < this.FrameCount; ++i) {
            var imageInfo = new SKImageInfo(codecInfo.Width, codecInfo.Height);
            var newBitmap = new SKBitmap(imageInfo);
            var pointer = newBitmap.GetPixels();
            var codecOptions = new SKCodecOptions(i);
            this.Codec.GetPixels(imageInfo, pointer, codecOptions);
            newBitmap.SetImmutable();
            this.EmoteFrames.Add(newBitmap);
        }
    }

    public void Resize(double newScale) {
        var codecInfo = this.Codec.Info;
        for (var i = 0; i < this.FrameCount; ++i) {
            var imageInfo = new SKImageInfo((int)(codecInfo.Width * newScale), (int)(codecInfo.Height * newScale));
            var newBitmap = new SKBitmap(imageInfo);
            this.EmoteFrames[i].ScalePixels(newBitmap, SKFilterQuality.High);
            this.EmoteFrames[i].Dispose();
            newBitmap.SetImmutable();
            this.EmoteFrames[i] = newBitmap;
        }
    }

    #region ImplementIDisposable

    public void Dispose() { this.Dispose(true); }

    private void Dispose(bool isDisposing) {
        try {
            if (this.Disposed)
                return;

            if (isDisposing) {
                foreach (var bitmap in this.EmoteFrames)
                    bitmap?.Dispose();

                this.Codec?.Dispose();
            }
        } finally {
            this.Disposed = true;
        }
    }

    #endregion

}
