using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of --collision (Overwrite / Exit / Rename / Prompt) when the output file already exists. Every verb that writes
    // files shares FileCollisionHandler; chatupdate is used because it is the fastest offline verb. These tests guard against the CLI
    // silently destroying an existing file, or hanging on a prompt when run from a script.
    public class FileCollisionTests : IDisposable
    {
        private const string EXISTING = "existing content";

        private readonly TempDirectory _temp = new();
        private readonly string _output;
        private static readonly string ChatJson = TestData.Path("E2E", "chat.json");

        public FileCollisionTests()
        {
            _output = _temp.Combine("out.txt");
            File.WriteAllText(_output, EXISTING);
        }

        public void Dispose() => _temp.Dispose();

        private CliResult Update(string? stdin, params string[] extraArgs) =>
            Cli.Run(_temp.Path, ["chatupdate", "-i", ChatJson, "-o", _output, ..extraArgs], stdin);

        // Overwrite must replace the existing file with the new output.
        [Fact]
        public void OverwriteReplacesExistingFile()
        {
            var result = Update(null, "--collision", "Overwrite");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.StartsWith("[0:00:00] E2E_Alpha", File.ReadAllText(_output));
        }

        // Exit must stop with exit code 1 and leave the existing file untouched.
        [Fact]
        public void ExitKeepsExistingFileAndFails()
        {
            var result = Update(null, "--collision", "Exit");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("already exists, exiting", result.StdOut);
            Assert.Equal(EXISTING, File.ReadAllText(_output));
        }

        // Rename must write to the first free "name (n).ext" and leave every existing file untouched.
        [Fact]
        public void RenameWritesToNextFreeName()
        {
            File.WriteAllText(_temp.Combine("out (1).txt"), EXISTING);

            var result = Update(null, "--collision", "Rename");

            Assert.True(result.ExitCode == 0, result.ToString());
            Assert.Contains("will be renamed to", result.StdOut);
            Assert.Equal(EXISTING, File.ReadAllText(_output));
            Assert.Equal(EXISTING, File.ReadAllText(_temp.Combine("out (1).txt")));
            Assert.StartsWith("[0:00:00] E2E_Alpha", File.ReadAllText(_temp.Combine("out (2).txt")));
        }

        // Prompt is the default. When stdin has no answer (scripts, CI) it must fail fast with a hint instead of hanging or overwriting.
        [Fact]
        public void PromptWithoutInputFailsWithoutWriting()
        {
            var result = Update(null);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("[O] Overwrite / [R] Rename / [E] Exit", result.StdOut);
            Assert.Contains("Could not read user input", result.StdOut);
            Assert.Equal(EXISTING, File.ReadAllText(_output));
        }

        // Each prompt answer must do what it says, answers are case-insensitive, and an unrecognised answer must re-prompt rather than
        // pick a default.
        [Theory]
        [InlineData("o\n", 0, "out.txt")]
        [InlineData("Overwrite\n", 0, "out.txt")]
        [InlineData("r\n", 0, "out (1).txt")]
        [InlineData("what\nR\n", 0, "out (1).txt")]
        [InlineData("e\n", 1, null)]
        public void PromptAnswerIsHonoured(string answer, int expectedExitCode, string? expectedWrittenFile)
        {
            var result = Update(answer);

            Assert.True(result.ExitCode == expectedExitCode, result.ToString());
            if (expectedWrittenFile is null)
            {
                Assert.Equal(EXISTING, File.ReadAllText(_output));
                Assert.Single(Directory.GetFiles(_temp.Path));
            }
            else
            {
                Assert.StartsWith("[0:00:00] E2E_Alpha", File.ReadAllText(_temp.Combine(expectedWrittenFile)));
            }

            if (expectedWrittenFile == "out (1).txt")
            {
                Assert.Equal(EXISTING, File.ReadAllText(_output));
            }
        }
    }
}
