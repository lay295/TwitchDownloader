using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class TwitchEmote
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

        public TwitchEmote(List<SKBitmap> Emote_frames, SKCodec Codec, string Name, string ImageType, string Id, int ImageScale, byte[] ImageData)
        {
            emote_frames = Emote_frames;
            codec = Codec;
            name = Name;
            id = Id;
            width = Emote_frames.First().Width;
            height = Emote_frames.First().Height;
            imageScale = ImageScale;
            imageData = ImageData;

            // If we are webp, with zero frame count then we are a static image
            // Thus we should just treat it as a differnt imageType so we don't animate it
            imageType = ImageType;
            if (imageType == "webp" && Codec.FrameCount == 0)
                imageType = "webp_static";

            // Split animated image into a list of images
            if (imageType == "gif" || imageType == "webp")
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
