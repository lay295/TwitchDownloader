using SkiaSharp;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.TwitchObjects
{
    [Flags]
    public enum ChatBadgeType
    {
        Other = 1,
        Broadcaster = 2,
        Moderator = 4,
        VIP = 8,
        Subscriber = 16,
        Predictions = 32,
        NoAudioVisual = 64,
        PrimeGaming = 128
    }

    public class ChatBadgeData
    {
        public string title { get; set; }
        public string description { get; set; }
        public byte[] bytes { get; set; }
        [JsonIgnore]
        public SKCodec Codec { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string url { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public sealed class ChatBadge : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public string Name;
        private readonly Dictionary<string, SKBitmap> _versionBitmaps;
        private Dictionary<string, SKImage> _versions;
        public Dictionary<string, SKImage> Versions => _versions ??= _versionBitmaps.ToDictionary(x => x.Key, x => SKImage.FromBitmap(x.Value));
        public readonly Dictionary<string, ChatBadgeData> VersionsData;
        public readonly ChatBadgeType Type;

        public ChatBadge(string name, Dictionary<string, ChatBadgeData> versions)
        {
            Name = name;
            _versionBitmaps = new Dictionary<string, SKBitmap>();
            VersionsData = versions;

            foreach (var (versionName, versionData) in versions)
            {
                SKBitmap badgeImage;
                if (versionData.Codec is null)
                {
                    // For some reason, twitch has corrupted images sometimes :) for example
                    // https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                    using var ms = new MemoryStream(versionData.bytes);
                    using var codec = SKCodec.Create(ms, out var result);
                    if (codec is null)
                        throw new Exception($"Skia was unable to decode badge {versionName} ({name}). Returned: {result}");

                    badgeImage = SKBitmap.Decode(codec);
                }
                else
                {
                    badgeImage = SKBitmap.Decode(versionData.Codec);
                }

                badgeImage.SetImmutable();
                _versionBitmaps.Add(versionName, badgeImage);
            }

            Type = name switch
            {
                "broadcaster" => ChatBadgeType.Broadcaster,
                "moderator" => ChatBadgeType.Moderator,
                "vip" => ChatBadgeType.VIP,
                "subscriber" => ChatBadgeType.Subscriber,
                "predictions" => ChatBadgeType.Predictions,
                "no_video" or "no_audio" => ChatBadgeType.NoAudioVisual,
                "premium" => ChatBadgeType.PrimeGaming,
                _ => ChatBadgeType.Other,
            };
        }

        /// <inheritdoc cref="TwitchEmote.SnapResize(int,int,int)"/>
        public void SnapResize(int height, int upSnapThreshold, int downSnapThreshold)
        {
            foreach (var (versionName, bitmap) in _versionBitmaps)
            {
                var bitmapInfo = bitmap.Info;

                var badgeHeight = TwitchHelper.SnapResizeHeight(height, upSnapThreshold, downSnapThreshold, bitmapInfo.Height);

                var imageInfo = new SKImageInfo((int)(badgeHeight / (double)bitmap.Height * bitmap.Width), badgeHeight);
                var newBitmap = new SKBitmap(imageInfo);
                bitmap.ScalePixels(newBitmap, SKFilterQuality.High);
                bitmap.Dispose();
                newBitmap.SetImmutable();
                _versionBitmaps[versionName] = newBitmap;
            }

            foreach (var (_, image) in _versions ?? [])
            {
                image?.Dispose();
            }
            _versions = null;
        }

        public void Scale(double newScale) => SnapScale(newScale, 0, 0);

        public void SnapScale(double newScale, int upSnapThreshold, int downSnapThreshold)
        {
            if (Math.Abs(newScale - 1) < 0.01)
            {
                return;
            }

            foreach (var (versionName, bitmap) in _versionBitmaps)
            {
                var bitmapInfo = bitmap.Info;
                var height = TwitchHelper.SnapResizeHeight((int)(bitmapInfo.Height * newScale), upSnapThreshold, downSnapThreshold, bitmapInfo.Height);

                var imageInfo = new SKImageInfo((int)(height / (double)bitmapInfo.Height * bitmapInfo.Width), height);
                var newBitmap = new SKBitmap(imageInfo);
                bitmap.ScalePixels(newBitmap, SKFilterQuality.High);
                bitmap.Dispose();
                newBitmap.SetImmutable();
                _versionBitmaps[versionName] = newBitmap;
            }

            foreach (var (_, image) in _versions ?? [])
            {
                image?.Dispose();
            }
            _versions = null;
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
                    foreach (var (_, image) in _versions ?? [])
                    {
                        image?.Dispose();
                    }
                    
                    foreach (var (_, bitmap) in _versionBitmaps)
                    {
                        bitmap?.Dispose();
                    }

                    foreach (var (_, badgeData) in VersionsData)
                    {
                        badgeData.Codec?.Dispose();
                    }
                }
            }
            finally
            {
                Disposed = true;
            }
        }
    }
}
