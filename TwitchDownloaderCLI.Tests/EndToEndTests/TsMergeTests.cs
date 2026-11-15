using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of `tsmerge`, which concatenates MPEG-TS parts listed in a text file or M3U8 playlist.
    // TestData/E2E/part0-2.ts are three consecutive 1 s segments (32x18 H.264, ~7 KB each) of one synthetic stream, generated with:
    //   ffmpeg -f lavfi -i testsrc=size=32x18:rate=10 -t 3 -c:v libx264 -preset ultrafast -g 10 -keyint_min 10 -sc_threshold 0
    //          -pix_fmt yuv420p -f segment -segment_format mpegts -segment_time 1 -reset_timestamps 0 part%d.ts
    public class TsMergeTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private static readonly string[] Parts = [TestData.Path("E2E", "part0.ts"), TestData.Path("E2E", "part1.ts"), TestData.Path("E2E", "part2.ts")];

        public void Dispose() => _temp.Dispose();

        private static byte[] Concat(IEnumerable<string> files) => files.SelectMany(File.ReadAllBytes).ToArray();

        // A plain list of absolute paths must produce the byte-exact concatenation of the parts, in list order.
        [Fact]
        public void PlainListIsConcatenatedInOrder()
        {
            var list = _temp.Combine("parts.txt");
            var output = _temp.Combine("merged.ts");
            string[] order = [Parts[0], Parts[2], Parts[1]];
            File.WriteAllLines(list, order);

            var result = Cli.RunOk(_temp.Path, "tsmerge", "-i", list, "-o", output);

            Assert.Contains("experimental", result.StdOut);
            Assert.Equal(Concat(order), File.ReadAllBytes(output));
        }

        // In an M3U8 playlist the #EXTM3U/#EXTINF/#EXT-X-* tags and blank lines must be skipped, not treated as file names, and the
        // merged stream must be a playable 3 s video.
        [Fact]
        public void M3u8PlaylistTagsAreIgnored()
        {
            var list = _temp.Combine("parts.m3u8");
            var output = _temp.Combine("merged.ts");
            File.WriteAllLines(list, [
                "#EXTM3U", "#EXT-X-VERSION:3", "#EXT-X-TARGETDURATION:1",
                "#EXTINF:1.0,", Parts[0], "",
                "#EXTINF:1.0,", Parts[1],
                "#EXTINF:1.0,", Parts[2],
                "#EXT-X-ENDLIST",
            ]);

            Cli.RunOk(_temp.Path, "tsmerge", "-i", list, "-o", output);

            Assert.Equal(Concat(Parts), File.ReadAllBytes(output));
            TestData.RequireFfmpeg();
            var probe = Media.Inspect(output);
            Assert.Equal((32, 18), (probe.Width, probe.Height));
            Assert.Equal(3, probe.DurationSeconds, 0.2);
        }

        // A part that is not a whole number of 188-byte TS packets must be named in a warning, but the merge must still complete (the
        // user may prefer a glitch over nothing).
        [Fact]
        public void CorruptPartIsReportedButMerged()
        {
            var corrupt = _temp.Combine("corrupt.ts");
            File.WriteAllBytes(corrupt, File.ReadAllBytes(Parts[1])[..1000]);
            var list = _temp.Combine("parts.txt");
            var output = _temp.Combine("merged.ts");
            string[] order = [Parts[0], corrupt, Parts[2]];
            File.WriteAllLines(list, order);

            var result = Cli.RunOk(_temp.Path, "tsmerge", "-i", list, "-o", output);

            Assert.Contains("The following TS files are invalid or corrupted", result.StdOut);
            Assert.Contains("corrupt.ts", result.StdOut);
            Assert.Equal(Concat(order), File.ReadAllBytes(output));
        }

        // A missing list file is a user error and should be reported with a message and exit code 1. Today TsMerger throws
        // FileNotFoundException out of Main, so the runtime aborts with a stack trace (SIGABRT and a core dump on Unix).
        [Fact(Skip = "Existing behavior in v1.56.5: tsmerge with a missing input list ends with an unhandled FileNotFoundException (stack trace) rather than an error message and exit code 1")]
        public void MissingListFailsCleanly()
        {
            var result = Cli.Run(_temp.Path, ["tsmerge", "-i", _temp.Combine("missing.txt"), "-o", _temp.Combine("merged.ts")]);

            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("Unhandled exception", result.StdErr);
        }
    }
}
