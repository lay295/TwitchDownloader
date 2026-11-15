using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ToolTests
{
    // Tests UserPrompt.ShowYesNo, the confirmation used before destructive actions (clearing the cache, self-updating).
    // A misread answer could delete data or replace the executable without consent.
    // In the process-state collection because it reads Console.In and writes Console.Out.
    [Collection(ProcessStateCollection.NAME)]
    public class UserPromptTests
    {
        // Short and long answers are accepted case-insensitively with surrounding whitespace, and the question is printed first.
        [Theory]
        [InlineData("y", UserPromptResult.Yes)]
        [InlineData("  YES ", UserPromptResult.Yes)]
        [InlineData("n", UserPromptResult.No)]
        [InlineData("No", UserPromptResult.No)]
        public void ReturnsAnswer(string input, UserPromptResult expected)
        {
            using var console = new ConsoleCapture(input + "\n");

            var result = UserPrompt.ShowYesNo("Proceed?");

            Assert.Equal(expected, result);
            Assert.Equal("Proceed?\n[Y] Yes / [N] No: ", console.Output);
        }

        // Unrecognised answers must re-prompt rather than being treated as yes or no.
        [Fact]
        public void InvalidAnswer_PromptsAgain()
        {
            using var console = new ConsoleCapture("maybe\n\nn\n");

            var result = UserPrompt.ShowYesNo("Proceed?");

            Assert.Equal(UserPromptResult.No, result);
            Assert.Equal(3, console.Output.Split("[Y] Yes / [N] No: ").Length - 1);
        }

        // When stdin is closed (e.g. piped/non-interactive), the prompt must give up with Unknown instead of looping forever, and report
        // the problem through the supplied logger.
        [Fact]
        public void ClosedInput_ReturnsUnknownAndLogsToLogger()
        {
            using var console = new ConsoleCapture("");
            var logger = new RecordingTaskProgress();

            var result = UserPrompt.ShowYesNo("Proceed?", logger);

            Assert.Equal(UserPromptResult.Unknown, result);
            Assert.Equal(["Could not read user input."], logger.Errors);
        }

        // Without a logger, the closed-input error must still be visible on the console.
        [Fact]
        public void ClosedInput_WithoutLogger_WritesErrorToConsole()
        {
            using var console = new ConsoleCapture("");

            var result = UserPrompt.ShowYesNo("Proceed?");

            Assert.Equal(UserPromptResult.Unknown, result);
            Assert.EndsWith("[ERROR] - Could not read user input.\n", console.Output);
        }
    }
}
