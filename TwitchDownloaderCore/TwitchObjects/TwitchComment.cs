using System.Collections.Generic;
using SkiaSharp;

namespace TwitchDownloaderCore.TwitchObjects;

public class TwitchComment {
    public string Section { get; set; }
    public double SecondsOffset { get; set; }
    public List<GifEmote> GifEmotes { get; set; }
    public List<SKBitmap> NormalEmotes { get; set; }
    public List<SKRect> NormalEmotesPositions { get; set; }
}
