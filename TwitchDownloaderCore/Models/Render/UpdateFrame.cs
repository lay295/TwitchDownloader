using SkiaSharp;

namespace TwitchDownloaderCore.Models.Render
{
    public class UpdateFrame
    {
        public SKBitmap Image { get; set; }
        public List<CommentSection> Comments { get; set; } = [];
        public int CommentIndex { get; set; }
    }
}
