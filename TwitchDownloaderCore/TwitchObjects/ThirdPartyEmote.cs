using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class ThirdPartyEmote
    {
        public List<SKBitmap> emote_frames = new List<SKBitmap>();
        public SKCodec codec;
        public byte[] imageData;
        public string imageType;
        public string name;
        public string id;
        public int width;
        public int height;
        public int imageScale;

        public ThirdPartyEmote(List<SKBitmap> Emote_frames, SKCodec Codec, string Name, string ImageType, string Id, int ImageScale, byte[] ImageData)
        {
            emote_frames = Emote_frames;
            codec = Codec;
            name = Name;
            imageType = ImageType;
            id = Id;
            width = Emote_frames.First().Width;
            height = Emote_frames.First().Height;
            imageScale = ImageScale;
            imageData = ImageData;

            if (imageType == "gif")
            {
                emote_frames.Clear();
                for (int i = 0; i < Codec.FrameCount; i++)
                {
                    SKImageInfo imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height);
                    SKBitmap newBitmap = new SKBitmap(imageInfo);
                    IntPtr pointer = newBitmap.GetPixels();
                    SKCodecOptions codecOptions = new SKCodecOptions(i);
                    codec.GetPixels(imageInfo, pointer, codecOptions);
                    emote_frames.Add(newBitmap);
                }
            }
        }

        public SKBitmap GetFrame(int frameNum)
        {
            SKImageInfo imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height);
            SKBitmap newBitmap = new SKBitmap(imageInfo);
            IntPtr pointer = newBitmap.GetPixels();
            SKCodecOptions codecOptions = new SKCodecOptions(frameNum);
            codec.GetPixels(imageInfo, pointer, codecOptions);
            return newBitmap;
        }
    }
}
