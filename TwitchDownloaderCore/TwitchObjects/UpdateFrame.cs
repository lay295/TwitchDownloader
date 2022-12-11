using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class UpdateFrame
    {
        public SKBitmap Image { get; set; }
        public List<CommentSection> Comments { get; set; } = new List<CommentSection>();
        public int CommentIndex { get; set; }
    }
}
