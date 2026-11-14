using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of `chatrender --offline` on the synthetic TestData/E2E/chat.json (10 s, 8 comments, embedded red first-party
    // emote, blue third-party emote and green badge). The output is checked with ffprobe and by sampling decoded pixels, so these tests
    // catch regressions anywhere in the pipeline: argument mapping, JSON parsing, embedded image decoding, Skia drawing and the ffmpeg pipe.
    // Renders are kept tiny (<= 200x200 at 10 fps) so each one takes about a second.
    public class ChatRenderTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private static readonly string ChatJson = TestData.Path("E2E", "chat.json");

        public void Dispose() => _temp.Dispose();

        private CliResult Render(string output, params string[] extraArgs) =>
            Cli.Run(_temp.Path, ["chatrender", "-i", ChatJson, "-o", output, "--offline", "--framerate", "10", "--emoji-vendor", "none", "--temp-path", _temp.Combine("cache"), ..extraArgs]);

        // An offline render must honour width/height/framerate, cover the chat's 0-10 s range, and actually draw the embedded first-party
        // emote, third-party emote and badge from the JSON (it must not need the network to find them; the dead proxy enforces this).
        [Fact]
        public void OfflineRenderDrawsEmbeddedImagesAtRequestedGeometry()
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("render.mp4");

            var result = Render(output, "-w", "200", "-h", "200");

            Assert.True(result.ExitCode == 0, result.ToString());
            var probe = Media.Inspect(output);
            Assert.Equal("h264", probe.VideoCodec);
            Assert.Equal((200, 200), (probe.Width, probe.Height));
            Assert.Equal(10, probe.Fps, 0.01);
            Assert.Equal(10, probe.DurationSeconds, 0.2);
            Assert.False(File.Exists(_temp.Combine("render_mask.mp4")));

            // At 3 s the first three comments are on screen: the badge (green), "nice <red emote>" and "third party <blue emote>".
            // The fixture's usernames are gold, so none of these colours can come from text.
            var rgb = Media.FrameRgb(output, 3, _temp.Path);
            Assert.True(CountPixels(rgb, (r, g, b) => r > 180 && g < 80 && b < 80) > 300, "first-party emote (red) was not drawn");
            Assert.True(CountPixels(rgb, (r, g, b) => b > 180 && r < 80 && g < 80) > 300, "third-party emote (blue) was not drawn");
            Assert.True(CountPixels(rgb, (r, g, b) => g > 100 && r < 80 && b < 80) > 100, "badge (green) was not drawn");
        }

        // --generate-mask must write "<name>_mask.<ext>" next to the render with the same geometry and length. With an opaque background
        // the mask is useless, so the CLI must warn about it, and must not warn when the background is transparent.
        [Theory]
        [InlineData("#111111", true)]
        [InlineData("#00000000", false)]
        public void GenerateMaskWritesMatchingMaskFile(string background, bool expectWarning)
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("render.mp4");

            var result = Render(output, "-w", "100", "-h", "100", "--generate-mask", "--background-color", background);

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal(expectWarning, result.StdOut.Contains("Generate mask option has been selected with an opaque background"));
            var render = Media.Inspect(output);
            var mask = Media.Inspect(_temp.Combine("render_mask.mp4"));
            Assert.Equal((render.Width, render.Height), (mask.Width, mask.Height));
            Assert.Equal(render.DurationSeconds, mask.DurationSeconds, 0.2);
        }

        // Odd dimensions break yuv420p encoding, so the CLI must warn and round them up to the next even number instead of failing.
        [Fact]
        public void OddDimensionsAreRoundedUpToEven()
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("render.mp4");

            var result = Render(output, "-w", "101", "-h", "99");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Contains("Width and Height MUST be even", result.StdOut);
            var probe = Media.Inspect(output);
            Assert.Equal((102, 100), (probe.Width, probe.Height));
        }

        // -b/-e together must limit the render to that time range.
        [Fact]
        public void BeginningAndEndingTrimTheRender()
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("render.mp4");

            var result = Render(output, "-w", "100", "-h", "100", "-b", "2", "-e", "5");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal(3, Media.Inspect(output).DurationSeconds, 0.2);
        }

        // -e on its own should end the render early, just like -b/-e together. ChatRenderer.GetVideoTicks only applies the overrides when
        // BOTH are set, so a lone -e (or -b) is silently ignored and the whole chat is rendered.
        [Fact(Skip = "Bug: chatrender ignores -e (and -b) unless both are given (ChatRenderer.GetVideoTicks requires StartOverride and EndOverride)")]
        public void EndingAloneTrimsTheRender()
        {
            TestData.RequireFfmpeg();
            var output = _temp.Combine("render.mp4");

            var result = Render(output, "-w", "100", "-h", "100", "-e", "4");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Equal(4, Media.Inspect(output).DurationSeconds, 0.2);
        }

        // --temp-path must be used as the image cache root (the renderer creates its TwitchDownloader cache folder there), so users can
        // keep the cache off a small system drive.
        [Fact]
        public void TempPathIsUsedForCache()
        {
            TestData.RequireFfmpeg();

            var result = Render(_temp.Combine("render.mp4"), "-w", "100", "-h", "100");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.True(Directory.Exists(_temp.Combine("cache", "TwitchDownloader")));
            Assert.False(Directory.Exists(_temp.Combine("systemp", "TwitchDownloader")), "the default (system temp) cache was used despite --temp-path");
        }

        private static int CountPixels(byte[] rgb, Func<byte, byte, byte, bool> match)
        {
            var count = 0;
            for (var i = 0; i + 2 < rgb.Length; i += 3)
            {
                if (match(rgb[i], rgb[i + 1], rgb[i + 2])) count++;
            }

            return count;
        }
    }
}
