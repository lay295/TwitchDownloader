﻿using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string url { get; set;}
    }

    [DebuggerDisplay("{Name}")]
    public sealed class ChatBadge : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public string Name;
        public Dictionary<string, SKBitmap> Versions;
        public Dictionary<string, ChatBadgeData> VersionsData;
        public ChatBadgeType Type;

        public ChatBadge(string name, Dictionary<string, ChatBadgeData> versions)
        {
            Name = name;
            Versions = new Dictionary<string, SKBitmap>();
            VersionsData = versions;

            foreach (var version in versions)
            {
                using MemoryStream ms = new MemoryStream(version.Value.bytes);
                //For some reason, twitch has corrupted images sometimes :) for example
                //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                SKBitmap badgeImage = SKBitmap.Decode(ms);
                Versions.Add(version.Key, badgeImage);
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

        public void Resize(double newScale)
        {
            List<string> keyList = new List<string>(Versions.Keys.ToList());

            for (int i = 0; i < keyList.Count; i++)
            {
                SKImageInfo imageInfo = new SKImageInfo((int)(Versions[keyList[i]].Width * newScale), (int)(Versions[keyList[i]].Height * newScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                Versions[keyList[i]].ScalePixels(newBitmap, SKFilterQuality.High);
                Versions[keyList[i]].Dispose();
                Versions[keyList[i]] = newBitmap;
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
