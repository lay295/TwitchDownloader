using SkiaSharp;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class CommentSection
    {
        public SKBitmap Image { get; set; }
        public List<(Point drawPoint, TwitchEmote emote)> Emotes { get; set; }
        public int CommentIndex { get; set; }
    }

    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
