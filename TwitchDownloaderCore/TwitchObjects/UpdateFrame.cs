using SkiaSharp;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class UpdateFrame
    {
        public SKBitmap Image { get; set; }
        public List<CommentSection> Comments { get; set; } = [];
        public int CommentIndex { get; set; }
    }
}
