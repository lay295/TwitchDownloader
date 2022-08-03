using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class CommentSection
    {
        public SKBitmap Image { get; set; }
        public List<(Point, TwitchEmote)> Emotes { get; set; } = new List<(Point, TwitchEmote)>();
    }

    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
