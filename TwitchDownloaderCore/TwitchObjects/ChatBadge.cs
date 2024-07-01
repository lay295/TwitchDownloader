using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.TwitchObjects
{
    [Flags]
    public enum ChatBadgeType
    {
        Other = 1,
        Broadcaster = 2,
        Moderator = 4,
        Vip = 8,
        Subscriber = 16,
        Predictions = 32,
        NoAudioVisual = 64,
        PrimeGaming = 128
    }

    public class ChatBadgeData
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public byte[] Bytes { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Url { get; set; }
    }

    [DebuggerDisplay("{Name}")]
    public sealed class ChatBadge : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public string Name;
        public readonly Dictionary<string, SKBitmap> Versions;
        public readonly Dictionary<string, ChatBadgeData> VersionsData;
        public readonly ChatBadgeType Type;

        public ChatBadge(string name, Dictionary<string, ChatBadgeData> versions)
        {
            this.Name = name;
            this.Versions = new Dictionary<string, SKBitmap>();
            this.VersionsData = versions;

            foreach (var (versionName, versionData) in versions)
            {
                using var ms = new MemoryStream(versionData.Bytes);

                //For some reason, twitch has corrupted images sometimes :) for example
                //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                using var codec = SKCodec.Create(ms, out var result);
                if (codec is not null) {
                    var badgeImage = SKBitmap.Decode(codec);
                    badgeImage.SetImmutable();
                    this.Versions.Add(versionName, badgeImage);
                } else
                    throw new Exception($"Skia was unable to decode badge {versionName} ({name}). Returned: {result}");

            }

            this.Type = name switch
            {
                "broadcaster" => ChatBadgeType.Broadcaster,
                "moderator" => ChatBadgeType.Moderator,
                "vip" => ChatBadgeType.Vip,
                "subscriber" => ChatBadgeType.Subscriber,
                "predictions" => ChatBadgeType.Predictions,
                "no_video" or "no_audio" => ChatBadgeType.NoAudioVisual,
                "premium" => ChatBadgeType.PrimeGaming,
                _ => ChatBadgeType.Other,
            };
        }

        public void Resize(double newScale)
        {
            foreach (var (versionName, bitmap) in this.Versions)
            {
                var imageInfo = new SKImageInfo((int)(bitmap.Width * newScale), (int)(bitmap.Height * newScale));
                var newBitmap = new SKBitmap(imageInfo);
                bitmap.ScalePixels(newBitmap, SKFilterQuality.High);
                bitmap.Dispose();
                newBitmap.SetImmutable();
                this.Versions[versionName] = newBitmap;
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
                    foreach (var (_, bitmap) in Versions)
                    {
                        bitmap?.Dispose();
                    }
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
