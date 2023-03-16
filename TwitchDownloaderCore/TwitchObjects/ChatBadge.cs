using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    public class ChatBadgeSKBitmapData
    {
        public string title { get; set; }
        public object description { get; set; }
        public SKBitmap bitmap { get; set; }
    }
    public class ChatBadgeByteData
    {
        public string title { get; set; }
        public object description { get; set; }
        public byte[] bytes { get; set; }
    }
    public class ChatBadge
    {
        public string Name;
        public Dictionary<string, ChatBadgeSKBitmapData> Versions;
        public Dictionary<string, ChatBadgeByteData> VersionsData;
        public ChatBadgeType Type;

        public ChatBadge(string name, Dictionary<string, ChatBadgeByteData> versions)
        {
            Name = name;
            Versions = new Dictionary<string, ChatBadgeSKBitmapData>();
            VersionsData = versions;

            foreach (var version in versions)
            {
                using MemoryStream ms = new MemoryStream(version.Value.bytes);
                //For some reason, twitch has corrupted images sometimes :) for example
                //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                SKBitmap badgeImage = SKBitmap.Decode(ms);
                Versions.Add(version.Key, new()
                {
                    title = version.Value.title,
                    description = version.Value.description,
                    bitmap = badgeImage
                });
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
                SKImageInfo imageInfo = new SKImageInfo((int)(Versions[keyList[i]].bitmap.Width * newScale), (int)(Versions[keyList[i]].bitmap.Height * newScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                Versions[keyList[i]].bitmap.ScalePixels(newBitmap, SKFilterQuality.High);
                Versions[keyList[i]].bitmap.Dispose();
                Versions[keyList[i]].bitmap = newBitmap;
            }
        }
    }
}
