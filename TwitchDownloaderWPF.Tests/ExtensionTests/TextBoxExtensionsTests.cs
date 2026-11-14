using System.Windows.Controls;
using TwitchDownloaderWPF.Extensions;
using TwitchDownloaderWPF.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.ExtensionTests
{
    // Tests TextBoxExtensions.TryInsertAtCaret, used by the Settings window and the chat render page to insert template parameters such as
    // {title} at the cursor when the user clicks a parameter button. TextBox must be created on an STA thread, hence StaThread.Run.
    // The "replace active selection" branch needs keyboard focus (a shown window) and is not covered.
    public class TextBoxExtensionsTests
    {
        // Verifies a null text box or empty text is rejected with false and nothing is inserted.
        // Why: callers use the return value to decide whether to show an error; the null case happens when the target box was not resolved.
        [Fact]
        public void TryInsertAtCaret_RejectsNullTextBoxOrEmptyText()
        {
            StaThread.Run(() =>
            {
                Assert.False(((TextBox?)null).TryInsertAtCaret("{title}"));

                var textBox = new TextBox { Text = "abc" };
                Assert.False(textBox.TryInsertAtCaret(""));
                Assert.Equal("abc", textBox.Text);
            });
        }

        // Verifies the text is inserted at the caret and the caret moves to just after the inserted text.
        // Why: users click several parameter buttons in a row to build a filename template; the caret must advance so each insert lands
        // after the previous one instead of in front of it.
        [Fact]
        public void TryInsertAtCaret_InsertsAtCaretAndAdvancesCaret()
        {
            StaThread.Run(() =>
            {
                var textBox = new TextBox { Text = "[] - {title}" };
                textBox.CaretIndex = 1;

                Assert.True(textBox.TryInsertAtCaret("{date}"));
                Assert.True(textBox.TryInsertAtCaret("{id}"));

                Assert.Equal("[{date}{id}] - {title}", textBox.Text);
                Assert.Equal(11, textBox.CaretIndex);
            });
        }
    }
}
