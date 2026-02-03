using System.Reflection;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // ReSharper disable once InconsistentNaming
    public class ColorVisibilityTests
    {
        // AdjustColorVisibility should be extracted and made public at some point, but for now just use reflection
        private static readonly MethodInfo AdjustVisibilityMethodInfo = typeof(ChatRenderer).GetMethod("AdjustColorVisibility", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly Func<SKColor, SKColor, SKColor> AdjustVisibilityDelegate = (Func<SKColor, SKColor, SKColor>)Delegate.CreateDelegate(typeof(Func<SKColor, SKColor, SKColor>), AdjustVisibilityMethodInfo);

        [Fact]
        public void RenderTestPattern()
        {
            const int HUE_LEN = 360; // Hue sweep length
            const int SAT_LEN = 100; // Sweep height
            const int TILE_W = HUE_LEN + 40; // Tile width
            const int TILE_H = SAT_LEN + 40; // Tile height
            const int TILE_X_OFFSET = (TILE_W - HUE_LEN) / 2; // Tile inner-sweep X offset
            const int TILE_Y_OFFSET = (TILE_H - SAT_LEN) / 2; // Tile inner-sweep Y offset

            var imageInfo = new SKImageInfo(TILE_W * 6, TILE_H * 2);
            using var bitmap = new SKBitmap(imageInfo);
            using var canvas = new SKCanvas(bitmap);
            using var paint = new SKPaint();
            paint.BlendMode = SKBlendMode.Src;

            var backgrounds = new[] { SKColors.Black, SKColors.White, SKColors.Gray, SKColors.Red, SKColors.Blue, SKColors.Lime };
            for (var i = 0; i < backgrounds.Length; i++)
            {
                var background = backgrounds[i];
                var tileStartX = TILE_W * i;
                DrawTile(tileStartX, TILE_H * 0, background, false); // Row 0 (reference)
                DrawTile(tileStartX, TILE_H * 1, background, true); // Row 1 (adjusted)

                background.ToHsv(out var bgHue, out var bgSat, out _);
                if (bgSat > 28)
                {
                    AssertPixelMatrix(tileStartX, bgHue, background);
                }
            }

            // WriteDebugBitmap(bitmap);

            return;

            void DrawTile(int startX, int startY, SKColor background, bool adjust)
            {
                paint.Color = background;
                canvas.DrawRect(startX, startY, TILE_W, TILE_H, paint);

                // Hue sweep
                for (var x = 0; x < HUE_LEN; x++)
                for (var y = 0; y < SAT_LEN; y++)
                {
                    var color = SKColor.FromHsv(x, y, 100);
                    if (adjust) color = AdjustVisibilityDelegate(color, background);

                    paint.Color = color;
                    canvas.DrawPoint(TILE_X_OFFSET + startX + x, TILE_Y_OFFSET + startY + y, paint);
                }

                // Value sweep
                for (var x = startX + TILE_X_OFFSET - 3; x < startX + TILE_X_OFFSET; x++)
                for (var y = 0; y < SAT_LEN; y++)
                {
                    var color = SKColor.FromHsv(0, 0, 100 - y);
                    if (adjust) color = AdjustVisibilityDelegate(color, background);

                    paint.Color = color;
                    canvas.DrawPoint(x, TILE_Y_OFFSET + startY + y, paint);
                }
            }

            void AssertPixelMatrix(int startX, float bgHue, SKColor background)
            {
                const int MATRIX_W = 9;
                const int MATRIX_H = 9;
                for (var x = -MATRIX_W; x <= MATRIX_W; x++)
                for (var y = -MATRIX_H; y <= MATRIX_H; y++)
                {
                    var x2 = x + (int)bgHue;
                    if (x2 < 0) x2 += HUE_LEN;
                    x2 %= HUE_LEN;

                    var y2 = y + SAT_LEN / 2;
                    if (y2 < 0) y2 += SAT_LEN;
                    y2 %= SAT_LEN;

                    var sourcePx = bitmap.GetPixel(
                        TILE_X_OFFSET + startX + x2,
                        TILE_Y_OFFSET + TILE_H * 0 + y2
                    );
                    var actualPx = bitmap.GetPixel(
                        TILE_X_OFFSET + startX + x2,
                        TILE_Y_OFFSET + TILE_H * 1 + y2
                    );

                    Assert.NotEqual(background, actualPx);
                    Assert.NotEqual(sourcePx, actualPx);
                }
            }
        }

        private static void WriteDebugBitmap(SKBitmap bitmap, string nameSuffix = "Output", [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "")
        {
            using var fs = new FileStream($"{Path.GetFileNameWithoutExtension(filePath)}.{methodName}_{nameSuffix}.png", FileMode.Create);
            bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
        }
    }
}