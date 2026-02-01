using System.Reflection;
using SkiaSharp;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // ReSharper disable once InconsistentNaming
    public class ColorVisibilityTests
    {
        // AdjustColorVisibility should be extracted and made public at some point, but for now just use reflection
        private static readonly MethodInfo AdjustVisibilityMethodInfo = typeof(ChatRenderer).GetMethod("AdjustColorVisibility", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly Func<SKColor, SKColor, SKColor> AdjustVisibilityDelegate = (Func<SKColor, SKColor, SKColor>)Delegate.CreateDelegate(typeof(Func<SKColor, SKColor, SKColor>), AdjustVisibilityMethodInfo);

        [SkippableFact]
        public void RenderTestPattern()
        {
            const int HUE_LEN = 360;
            const int SAT_LEN = 100;
            const int TILE_W = HUE_LEN + 40;
            const int TILE_H = SAT_LEN + 40;

            var imageInfo = new SKImageInfo(TILE_W * 6, TILE_H * 2);
            using var bitmap = new SKBitmap(imageInfo);
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.Src;

            // Row 0 (reference)
            DrawTile(TILE_W * 0, TILE_H * 0, SKColors.Black, false);
            DrawTile(TILE_W * 1, TILE_H * 0, SKColors.White, false);
            DrawTile(TILE_W * 2, TILE_H * 0, SKColors.Gray, false);
            DrawTile(TILE_W * 3, TILE_H * 0, SKColors.Red, false);
            DrawTile(TILE_W * 4, TILE_H * 0, SKColors.Blue, false);
            DrawTile(TILE_W * 5, TILE_H * 0, SKColors.Lime, false);

            // Row 1 (adjusted)
            DrawTile(TILE_W * 0, TILE_H * 1, SKColors.Black, true);
            DrawTile(TILE_W * 1, TILE_H * 1, SKColors.White, true);
            DrawTile(TILE_W * 2, TILE_H * 1, SKColors.Gray, true);
            DrawTile(TILE_W * 3, TILE_H * 1, SKColors.Red, true);
            DrawTile(TILE_W * 4, TILE_H * 1, SKColors.Blue, true);
            DrawTile(TILE_W * 5, TILE_H * 1, SKColors.Lime, true);

            try
            {
                using var fs = new FileStream($"{nameof(ColorVisibilityTests)}.{nameof(RenderTestPattern)}_Output.png", FileMode.Create);
                bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
            }
            catch (Exception ex)
            {
                Skip.If(true, $"Failed to write output file: {ex.Message}");
            }

            return;

            void DrawTile(int startX, int startY, SKColor background, bool adjust)
            {
                paint.Color = background;
                canvas.DrawRect(startX, startY, TILE_W, TILE_H, paint);

                const int X_OFFSET = (TILE_W - HUE_LEN) / 2;
                const int Y_OFFSET = (TILE_H - SAT_LEN) / 2;

                // Hue sweep
                for (var x = 0; x < HUE_LEN; x++)
                for (var y = 0; y < SAT_LEN; y++)
                {
                    var color = SKColor.FromHsv(x, y, 100);
                    if (adjust) color = AdjustVisibilityDelegate(color, background);

                    paint.Color = color;
                    canvas.DrawPoint(X_OFFSET + startX + x, Y_OFFSET + startY + y, paint);
                }

                // Value sweep
                for (var x = startX + X_OFFSET - 3; x < startX + X_OFFSET; x++)
                for (var y = 0; y < SAT_LEN; y++)
                {
                    var color = SKColor.FromHsv(0, 0, 100 - y);
                    if (adjust) color = AdjustVisibilityDelegate(color, background);

                    paint.Color = color;
                    canvas.DrawPoint(x, Y_OFFSET + startY + y, paint);
                }
            }
        }
    }
}