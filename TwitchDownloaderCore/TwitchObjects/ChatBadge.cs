using NeoSmart.Unicode;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;

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

    public class ChatBadge
    {
        public string Name;
        public Dictionary<string, SKBitmap> Versions;
        public Dictionary<string, byte[]> VersionsData;
        public ChatBadgeType Type;

        public ChatBadge(string name, Dictionary<string, byte[]> versions)
        {
            Name = name;
            Versions = new Dictionary<string, SKBitmap>();
            VersionsData = versions;

            foreach (var version in versions)
            {
                using MemoryStream ms = new MemoryStream(version.Value);
                //For some reason, twitch has corrupted images sometimes :) for example
                //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                SKBitmap badgeImage = SKBitmap.Decode(ms);
                Versions.Add(version.Key, badgeImage);
            }

            switch (name)
            {
                case "broadcaster":
                    Type = ChatBadgeType.Broadcaster;
                    break;
                case "moderator":
                    Type = ChatBadgeType.Moderator;
                    break;
                case "vip":
                    Type = ChatBadgeType.VIP;
                    break;
                case "subscriber":
                    Type = ChatBadgeType.Subscriber;
                    break;
                case "predictions":
                    Type = ChatBadgeType.Predictions;
                    break;
                case "no_video":
                case "no_audio":
                    Type = ChatBadgeType.NoAudioVisual;
                    break;
                case "premium":
                    Type = ChatBadgeType.PrimeGaming;
                    break;
                default:
                    Type = ChatBadgeType.Other;
                    break;
            }
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
    }
}
