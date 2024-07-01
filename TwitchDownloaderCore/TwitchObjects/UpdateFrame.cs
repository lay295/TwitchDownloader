using System.Collections.Generic;
using SkiaSharp;

namespace TwitchDownloaderCore.TwitchObjects;

public class UpdateFrame {
    public SKBitmap Image { get; set; }
    public List<CommentSection> Comments { get; set; } = new();
    public int CommentIndex { get; set; }
}
