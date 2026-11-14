using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests TsMerger, which byte-concatenates the .ts parts listed in a text/m3u8 file into one transport stream.
    // It is how users recover VODs from a partial/manual download, so ordering, list parsing and clean-up on failure matter.
    public class TsMergerTests
    {
        private static readonly byte[] Part0 = FakeTwitch.TsSegment(0);
        private static readonly byte[] Part1 = FakeTwitch.TsSegment(1);
        private static readonly byte[] Part2 = FakeTwitch.TsSegment(2);

        private static (string list, string output, string[] parts) WriteParts(TempDirectory temp, string listContent, params byte[][] partData)
        {
            var parts = new string[partData.Length];
            for (var i = 0; i < partData.Length; i++)
            {
                parts[i] = temp.Combine($"{i}.ts");
                File.WriteAllBytes(parts[i], partData[i]);
            }

            var list = temp.Combine("list.txt");
            File.WriteAllText(list, string.Format(listContent, parts.Cast<object>().ToArray()));
            return (list, temp.Combine("out", "merged.ts"), parts);
        }

        /// <summary>
        /// A plain newline separated list is merged in list order (not file-name order), blank lines are ignored and the
        /// missing output directory is created. Guards against reordering or dropping parts, which would corrupt the video.
        /// </summary>
        [Fact]
        public async Task MergesPlainListInListOrder()
        {
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "{2}\n\n{0}\n  \n{1}\n", Part0, Part1, Part2);
            var progress = new RecordingTaskProgress();

            await new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, progress).MergeAsync(CancellationToken.None);

            Assert.Equal(Part2.Concat(Part0).Concat(Part1).ToArray(), await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
            Assert.Equal(100, progress.Percentages.Last());
            Assert.Empty(progress.Info); // Every part is a valid TS, nothing to report
        }

        /// <summary>
        /// When the list is an m3u8 playlist, the #EXT tag lines are skipped and only the segment paths are merged.
        /// Guards against tags being treated as file paths (which would throw FileNotFound).
        /// </summary>
        [Fact]
        public async Task MergesM3U8Playlist_SkippingTags()
        {
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "#EXTM3U\n#EXT-X-TARGETDURATION:1\n#EXTINF:1.000,\n{0}\n#EXTINF:1.000,\n{1}\n#EXT-X-ENDLIST\n", Part0, Part1);

            await new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, new RecordingTaskProgress()).MergeAsync(CancellationToken.None);

            Assert.Equal(Part0.Concat(Part1).ToArray(), await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// '#' lines are only skipped for m3u8 lists. In a plain list (no #EXTM3U header) they are file names, so a missing
        /// "#..." file makes the merge fail, and the still-empty output is deleted rather than left behind.
        /// </summary>
        [Fact]
        public async Task PlainList_HashLinesAreFilePaths_FailAndCleanUpOutput()
        {
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "#not-a-comment\n{0}\n", Part0);

            await Assert.ThrowsAnyAsync<IOException>(() => new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, new RecordingTaskProgress()).MergeAsync(CancellationToken.None));

            Assert.False(File.Exists(output));
        }

        /// <summary>
        /// A part whose length is not a multiple of the 188-byte TS packet size is reported (by name) but still merged,
        /// so the user knows which part may glitch without losing the rest of the video.
        /// </summary>
        [Fact]
        public async Task ReportsCorruptPart_ButStillMergesIt()
        {
            using var temp = new TempDirectory();
            var corrupt = Part1[..^7];
            var (list, output, parts) = WriteParts(temp, "{0}\n{1}\n{2}\n", Part0, corrupt, Part2);
            var progress = new RecordingTaskProgress();

            await new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, progress).MergeAsync(CancellationToken.None);

            var info = Assert.Single(progress.Info);
            Assert.Contains(parts[1], info);
            Assert.DoesNotContain(parts[0], info);
            Assert.Equal(Part0.Length + corrupt.Length + Part2.Length, new FileInfo(output).Length);
        }

        /// <summary>
        /// If every part looks corrupt the check is assumed to be a false positive (e.g. a non-TS container) and nothing is
        /// logged. Guards the deliberate "don't cry wolf" behaviour.
        /// </summary>
        [Fact]
        public async Task AllPartsInvalid_IsTreatedAsFalsePositive()
        {
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "{0}\n{1}\n", [1, 2, 3], [4, 5]);
            var progress = new RecordingTaskProgress();

            await new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, progress).MergeAsync(CancellationToken.None);

            Assert.Empty(progress.Info);
            Assert.Equal([1, 2, 3, 4, 5], await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
        }

        /// <summary>A missing list file fails fast with FileNotFoundException and never creates the output file.</summary>
        [Fact]
        public async Task MissingInputFile_Throws_WithoutCreatingOutput()
        {
            using var temp = new TempDirectory();
            var output = temp.Combine("merged.ts");

            await Assert.ThrowsAsync<FileNotFoundException>(() => new TsMerger(new TsMergeOptions { InputFile = temp.Combine("nope.txt"), OutputFile = output }, new RecordingTaskProgress()).MergeAsync(CancellationToken.None));

            Assert.False(File.Exists(output));
        }

        /// <summary>
        /// Cancellation stops the merge with OperationCanceledException and the (still empty) output is cleaned up, so a
        /// cancelled task never leaves a 0-byte video behind.
        /// </summary>
        [Fact]
        public async Task Cancellation_ThrowsAndRemovesEmptyOutput()
        {
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "{0}\n{1}\n", Part0, Part1);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, new RecordingTaskProgress()).MergeAsync(new CancellationToken(true)));

            Assert.False(File.Exists(output));
        }

        /// <summary>
        /// When the output exists, the collision callback decides the destination: a renamed FileInfo is honoured (original
        /// untouched), and returning null aborts with FileNotFoundException. Guards against silently overwriting user files.
        /// </summary>
        [Fact]
        public async Task FileCollisionCallback_RenamesOrAborts()
        {
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "{0}\n", Part0);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, "existing", TestContext.Current.CancellationToken);
            var renamed = temp.Combine("out", "merged (1).ts");

            await new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output, FileCollisionCallback = _ => new FileInfo(renamed) }, new RecordingTaskProgress()).MergeAsync(CancellationToken.None);

            Assert.Equal("existing", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
            Assert.Equal(Part0, await File.ReadAllBytesAsync(renamed, TestContext.Current.CancellationToken));

            await Assert.ThrowsAsync<FileNotFoundException>(() => new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output, FileCollisionCallback = _ => null! }, new RecordingTaskProgress()).MergeAsync(CancellationToken.None));
            Assert.Equal("existing", await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// End to end with real MPEG-TS segments: the merged file is a playable stream whose duration is the sum of the
        /// parts (3 x 1s). Guards against the merge producing a byte-correct but unplayable file.
        /// </summary>
        [Fact]
        public async Task MergedSegments_ArePlayable()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var (list, output, _) = WriteParts(temp, "{0}\n{1}\n{2}\n", Part0, Part1, Part2);

            await new TsMerger(new TsMergeOptions { InputFile = list, OutputFile = output }, new RecordingTaskProgress()).MergeAsync(CancellationToken.None);

            var (_, report) = MediaProbe.Probe(output);
            Assert.InRange(MediaProbe.DurationSeconds(report), 2.8, 3.5);
            Assert.Contains("Video: h264", report);
            Assert.Contains("Audio: aac", report);
        }
    }
}
