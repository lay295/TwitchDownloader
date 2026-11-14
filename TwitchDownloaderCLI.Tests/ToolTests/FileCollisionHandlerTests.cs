using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ToolTests
{
    // Tests FileCollisionHandler, the callback every CLI verb hands to Core to decide what happens when the output file already exists
    // (--collision Overwrite/Rename/Prompt/Exit). Getting it wrong silently destroys a user's previous download.
    // The Exit behaviour and the prompt's "exit"/closed-stdin answers call Environment.Exit, which would kill the test host, so they are not tested here.
    [Collection(ProcessStateCollection.NAME)]
    public class FileCollisionHandlerTests
    {
        private static FileCollisionHandler CreateHandler(OverwriteBehavior behavior, RecordingTaskProgress? logger = null) =>
            new(new TsMergeArgs { OverwriteBehavior = behavior }, logger ?? new RecordingTaskProgress());

        // Overwrite must hand back the very same file so Core overwrites it, without prompting.
        [Fact]
        public void Overwrite_ReturnsSameFile()
        {
            using var temp = new TempDirectory();
            var file = new FileInfo(temp.Combine("out.mp4"));

            Assert.Same(file, CreateHandler(OverwriteBehavior.Overwrite).HandleCollisionCallback(file));
        }

        // Rename must pick the first free "name (n).ext" in the same directory so the existing file is preserved.
        [Fact]
        public void Rename_ReturnsNonCollidingName()
        {
            using var temp = new TempDirectory();
            File.WriteAllText(temp.Combine("out.mp4"), "");
            File.WriteAllText(temp.Combine("out (1).mp4"), "");

            var result = CreateHandler(OverwriteBehavior.Rename).HandleCollisionCallback(new FileInfo(temp.Combine("out.mp4")));

            Assert.Equal(temp.Combine("out (2).mp4"), result.FullName);
        }

        // Prompt asks the user and honours the answer ("o"/"overwrite" keeps the file, "r"/"rename" picks a new name), case-insensitively.
        [Theory]
        [InlineData("o", false)]
        [InlineData(" Overwrite ", false)]
        [InlineData("R", true)]
        [InlineData("rename", true)]
        public void Prompt_HonoursAnswer(string answer, bool expectRename)
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("out.mp4");
            File.WriteAllText(path, "");
            using var console = new ConsoleCapture(answer + "\n");

            var result = CreateHandler(OverwriteBehavior.Prompt).HandleCollisionCallback(new FileInfo(path));

            Assert.Equal(expectRename ? temp.Combine("out (1).mp4") : path, result.FullName);
            Assert.Equal($"The file '{path}' already exists.\n[O] Overwrite / [R] Rename / [E] Exit: ", console.Output);
        }

        // An unrecognised answer must re-prompt instead of defaulting to overwrite.
        [Fact]
        public void Prompt_InvalidAnswer_PromptsAgain()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("out.mp4");
            using var console = new ConsoleCapture("x\no\n");

            CreateHandler(OverwriteBehavior.Prompt).HandleCollisionCallback(new FileInfo(path));

            Assert.Equal(2, console.Output.Split("[O] Overwrite / [R] Rename / [E] Exit: ").Length - 1);
        }

        // An undefined behaviour value must fail loudly rather than fall through to some default action.
        [Fact]
        public void UnknownBehavior_Throws()
        {
            using var temp = new TempDirectory();

            Assert.Throws<ArgumentOutOfRangeException>(() => CreateHandler((OverwriteBehavior)42).HandleCollisionCallback(new FileInfo(temp.Combine("a"))));
        }
    }
}
