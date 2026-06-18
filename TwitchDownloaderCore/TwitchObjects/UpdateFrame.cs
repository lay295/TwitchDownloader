using SkiaSharp;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class UpdateFrame
    {
        public SKBitmap Image { get; set; }
        public List<CommentSection> Comments { get; set; } = new List<CommentSection>();
        public int CommentIndex { get; set; }
    }
}
