using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests MergeTs.Merge, the `tsmerge` verb that concatenates MPEG-TS parts listed in a text/M3U8 file. The merge is pure file I/O,
    // so the whole verb (argument mapping, collision handling, Core merger) is exercised end to end offline. --log-level None keeps
    // CliTaskProgress off the console so the tests can run in parallel.
    public class MergeTsTests
    {
        // 188 bytes is one MPEG-TS packet, the minimum the merger's part verification accepts as a valid part.
        private static string WritePart(TempDirectory temp, string name, byte fill)
        {
            var path = temp.Combine(name);
            File.WriteAllBytes(path, Enumerable.Repeat(fill, 188).ToArray());
            return path;
        }

        // The parts listed in the input file (blank lines ignored) are concatenated in order into the output file.
        [Fact]
        public void Merge_ConcatenatesPartsInOrder()
        {
            using var temp = new TempDirectory();
            var part1 = WritePart(temp, "1.ts", 1);
            var part2 = WritePart(temp, "2.ts", 2);
            var list = temp.Combine("list.txt");
            File.WriteAllLines(list, [part2, "", part1]);
            var output = temp.Combine("out.ts");

            MergeTs.Merge(CliArgs.Parse<TsMergeArgs>("tsmerge", "-i", list, "-o", output, "--log-level", "None"));

            Assert.Equal([..File.ReadAllBytes(part2), ..File.ReadAllBytes(part1)], File.ReadAllBytes(output));
        }

        // M3U8 playlists are accepted as input lists: their #-prefixed tag lines must be skipped instead of being treated as file paths.
        [Fact]
        public void Merge_AcceptsM3U8Playlist()
        {
            using var temp = new TempDirectory();
            var part1 = WritePart(temp, "1.ts", 1);
            var list = temp.Combine("list.m3u8");
            File.WriteAllLines(list, ["#EXTM3U", "#EXTINF:10.000,", part1, "#EXT-X-ENDLIST"]);
            var output = temp.Combine("out.ts");

            MergeTs.Merge(CliArgs.Parse<TsMergeArgs>("tsmerge", "-i", list, "-o", output, "--log-level", "None"));

            Assert.Equal(File.ReadAllBytes(part1), File.ReadAllBytes(output));
        }

        // The --collision behaviour must reach Core: with Rename an existing output file is left untouched and a new name is used.
        [Fact]
        public void Merge_HonoursCollisionBehavior()
        {
            using var temp = new TempDirectory();
            var part1 = WritePart(temp, "1.ts", 1);
            var list = temp.Combine("list.txt");
            File.WriteAllLines(list, [part1]);
            var output = temp.Combine("out.ts");
            File.WriteAllText(output, "keep me");

            MergeTs.Merge(CliArgs.Parse<TsMergeArgs>("tsmerge", "-i", list, "-o", output, "--collision", "Rename", "--log-level", "None"));

            Assert.Equal("keep me", File.ReadAllText(output));
            Assert.Equal(File.ReadAllBytes(part1), File.ReadAllBytes(temp.Combine("out (1).ts")));
        }

        // A missing input list must surface as an error (FileNotFoundException from Core) rather than creating an empty output.
        [Fact]
        public void Merge_MissingInputList_Throws()
        {
            using var temp = new TempDirectory();

            var ex = Assert.Throws<AggregateException>(() =>
                MergeTs.Merge(CliArgs.Parse<TsMergeArgs>("tsmerge", "-i", temp.Combine("missing.txt"), "-o", temp.Combine("out.ts"), "--log-level", "None")));

            Assert.IsType<FileNotFoundException>(ex.InnerException);
            Assert.False(File.Exists(temp.Combine("out.ts")));
        }
    }
}
