using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI.Tests.ToolTests
{
    public class UserPromptTests
    {
        [Theory]
        [InlineData("y")]
        [InlineData("Y")]
        [InlineData("yes")]
        [InlineData("YES")]
        [InlineData(" y ")]
        [InlineData("\tYes\t")]
        public void ShowYesNoReturnsYesForAffirmativeInput(string userInput)
        {
            using var input = new StringReader(userInput + Environment.NewLine);

            var result = UserPrompt.ShowYesNo("Continue?", input, null);

            Assert.Equal(UserPromptResult.Yes, result);
        }

        [Theory]
        [InlineData("n")]
        [InlineData("N")]
        [InlineData("no")]
        [InlineData("NO")]
        [InlineData(" n ")]
        [InlineData("\tNo\t")]
        public void ShowYesNoReturnsNoForNegativeInput(string userInput)
        {
            using var input = new StringReader(userInput + Environment.NewLine);

            var result = UserPrompt.ShowYesNo("Continue?", input, null);

            Assert.Equal(UserPromptResult.No, result);
        }

        [Fact]
        public void ShowYesNoRepromptsAfterUnrecognizedInput()
        {
            using var input = new StringReader($"maybe{Environment.NewLine}y{Environment.NewLine}");

            var result = UserPrompt.ShowYesNo("Continue?", input, null);

            Assert.Equal(UserPromptResult.Yes, result);
        }

        [Fact]
        public void ShowYesNoReturnsUnknownAtEndOfInput()
        {
            using var input = new StringReader("");

            var result = UserPrompt.ShowYesNo("Continue?", input, null);

            Assert.Equal(UserPromptResult.Unknown, result);
        }

        [Fact]
        public void ShowYesNoReturnsUnknownWhenNoInputIsAvailable()
        {
            var result = UserPrompt.ShowYesNo("Continue?", null, null);

            Assert.Equal(UserPromptResult.Unknown, result);
        }

        [Theory]
        [InlineData("o")]
        [InlineData("O")]
        [InlineData("overwrite")]
        [InlineData("OVERWRITE")]
        [InlineData(" o ")]
        [InlineData("\tOverwrite\t")]
        public void ShowOverwriteRenameExitReturnsOverwriteForOverwriteInput(string userInput)
        {
            using var input = new StringReader(userInput + Environment.NewLine);

            var result = UserPrompt.ShowOverwriteRenameExit("The file already exists.", input);

            Assert.Equal(OverwriteBehavior.Overwrite, result);
        }

        [Theory]
        [InlineData("r")]
        [InlineData("R")]
        [InlineData("rename")]
        [InlineData("RENAME")]
        [InlineData(" r ")]
        [InlineData("\tRename\t")]
        public void ShowOverwriteRenameExitReturnsRenameForRenameInput(string userInput)
        {
            using var input = new StringReader(userInput + Environment.NewLine);

            var result = UserPrompt.ShowOverwriteRenameExit("The file already exists.", input);

            Assert.Equal(OverwriteBehavior.Rename, result);
        }

        [Theory]
        [InlineData("e")]
        [InlineData("E")]
        [InlineData("exit")]
        [InlineData("EXIT")]
        [InlineData(" e ")]
        [InlineData("\tExit\t")]
        public void ShowOverwriteRenameExitReturnsExitForExitInput(string userInput)
        {
            using var input = new StringReader(userInput + Environment.NewLine);

            var result = UserPrompt.ShowOverwriteRenameExit("The file already exists.", input);

            Assert.Equal(OverwriteBehavior.Exit, result);
        }

        [Fact]
        public void ShowOverwriteRenameExitRepromptsAfterUnrecognizedInput()
        {
            using var input = new StringReader($"maybe{Environment.NewLine}r{Environment.NewLine}");

            var result = UserPrompt.ShowOverwriteRenameExit("The file already exists.", input);

            Assert.Equal(OverwriteBehavior.Rename, result);
        }

        [Fact]
        public void ShowOverwriteRenameExitReturnsNullAtEndOfInput()
        {
            using var input = new StringReader("");

            var result = UserPrompt.ShowOverwriteRenameExit("The file already exists.", input);

            Assert.Null(result);
        }

        [Fact]
        public void ShowOverwriteRenameExitReturnsNullWhenNoInputIsAvailable()
        {
            var result = UserPrompt.ShowOverwriteRenameExit("The file already exists.", null);

            Assert.Null(result);
        }
    }
}