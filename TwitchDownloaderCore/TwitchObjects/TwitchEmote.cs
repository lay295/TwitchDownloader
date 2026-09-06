using SkiaSharp;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace TwitchDownloaderCore.TwitchObjects
{
    public enum EmoteProvider
    {
        FirstParty,
        ThirdParty
    }

    // Frames are decoded lazily and can be released again: a long animated 7TV emote is a few hundred KB compressed
    // but tens of MB as bitmaps, and a busy channel has hundreds. Size and timings come from the codec header.
    [DebuggerDisplay("{Name}")]
    public sealed class TwitchEmote : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public SKCodec Codec { get; }
        public byte[] ImageData { get; set; }
        public EmoteProvider EmoteProvider { get; set; }

        private List<SKBitmap> _emoteBitmaps;
        private SKImage[] _emoteFrames;
        private SKImageInfo _info;

        private List<SKBitmap> EmoteBitmaps => _emoteBitmaps ??= ExtractFrames();
        public SKImage[] EmoteFrames => _emoteFrames ??= EmoteBitmaps.Select(SKImage.FromBitmap).ToArray();

        public List<int> EmoteFrameDurations { get; private set; } = [];
        public int TotalDuration { get; set; }
        public string Name { get; }
        public string Id { get; }
        // Only set for images resolved at runtime, such as chat GIFs
        public string Url { get; set; }
        public int ImageScale { get; }
        public bool IsZeroWidth { get; set; }
        public int FrameCount { get; }
        public int Height => Info.Height;
        public int Width => Info.Width;

        // The size the emote draws at, known from the header without decoding a frame
        public SKImageInfo Info => _info;
        public bool FramesMaterialized => _emoteBitmaps is not null;
        public long DecodedByteSize => (long)_info.Width * _info.Height * 4 * FrameCount;
        // Stamped by the renderer as it draws, so the least recently used can be released
        public long LastUsedTick { get; set; }

        public TwitchEmote(byte[] imageData, [AllowNull] SKCodec codec, EmoteProvider emoteProvider, int imageScale, string imageId, string imageName, bool isZeroWidth = false)
        {
            if (codec is null)
            {
                var ms = new MemoryStream(imageData);
                Codec = SKCodec.Create(ms, out var result);
                if (Codec is null)
                {
                    throw new Exception($"Skia was unable to decode {imageName} ({imageId}). Returned: {result}");
                }
            }
            else
            {
                Codec = codec;
            }

            EmoteProvider = emoteProvider;
            Id = imageId;
            Name = imageName;
            ImageScale = imageScale;
            ImageData = imageData;
            IsZeroWidth = isZeroWidth;
            FrameCount = Math.Max(1, Codec.FrameCount);

            var codecInfo = Codec.Info;
            _info = new SKImageInfo(codecInfo.Width, codecInfo.Height);

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

        // Decodes every frame, scaling straight to Info so a full sized copy of the animation is never held
        private List<SKBitmap> ExtractFrames()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);

            var codecInfo = Codec.Info;
            var scaled = _info.Width != codecInfo.Width || _info.Height != codecInfo.Height;
            var bitmaps = new List<SKBitmap>(FrameCount);

            for (int i = 0; i < FrameCount; i++)
            {
                var nativeInfo = new SKImageInfo(codecInfo.Width, codecInfo.Height);
                var nativeBitmap = new SKBitmap(nativeInfo);
                Codec.GetPixels(nativeInfo, nativeBitmap.GetPixels(), new SKCodecOptions(i));

                if (!scaled)
                {
                    nativeBitmap.SetImmutable();
                    bitmaps.Add(nativeBitmap);
                    continue;
                }

                var newBitmap = new SKBitmap(_info);
                nativeBitmap.ScalePixels(newBitmap, SKFilterQuality.High);
                nativeBitmap.Dispose();
                newBitmap.SetImmutable();
                bitmaps.Add(newBitmap);
            }

            return bitmaps;
        }

        /// <summary>Drops the decoded frames. They are decoded again if the emote is drawn later.</summary>
        public void ReleaseFrames()
        {
            foreach (var image in _emoteFrames ?? [])
            {
                image?.Dispose();
            }
            _emoteFrames = null;

            foreach (var bitmap in _emoteBitmaps ?? [])
            {
                bitmap?.Dispose();
            }
            _emoteBitmaps = null;
        }

        /// <summary>
        /// Resizes the emote to have a <see cref="Height"/> of <paramref name="height"/>.
        /// If the nearest integer scale is within <paramref name="upSnapThreshold"/> or <paramref name="downSnapThreshold"/> of <paramref name="height"/>, it will be integer scaled instead.
        /// </summary>
        public void SnapResize(int height, int upSnapThreshold, int downSnapThreshold)
        {
            var codecInfo = Codec.Info;

            height = TwitchHelper.SnapResizeHeight(height, upSnapThreshold, downSnapThreshold, codecInfo.Height);

            SetTargetSize(new SKImageInfo((int)(height / (double)codecInfo.Height * codecInfo.Width), height));
        }

        public void Scale(double newScale) => SnapScale(newScale, 0, 0);

        public void SnapScale(double newScale, int upSnapThreshold, int downSnapThreshold)
        {
            if (Math.Abs(newScale - 1) < 0.01)
            {
                return;
            }

            var codecInfo = Codec.Info;
            var height = TwitchHelper.SnapResizeHeight((int)(codecInfo.Height * newScale), upSnapThreshold, downSnapThreshold, codecInfo.Height);

            SetTargetSize(new SKImageInfo((int)(height / (double)codecInfo.Height * codecInfo.Width), height));
        }

        // Records the size to decode at; anything already decoded is dropped so it comes back at the new size
        private void SetTargetSize(SKImageInfo info)
        {
            if (info.Width == _info.Width && info.Height == _info.Height)
            {
                return;
            }

            _info = info;
            ReleaseFrames();
        }

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
                    ReleaseFrames();
                    Codec?.Dispose();
                }
            }
            finally
            {
                Disposed = true;
            }
        }
    }
}
