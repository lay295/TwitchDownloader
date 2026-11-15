using System.Windows.Controls;
using TwitchDownloaderWPF.Behaviors;
using TwitchDownloaderWPF.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.BehaviorTests
{
    // Tests TextBoxTripleClickBehavior.GetCurrentLine, the pure part of the triple-click-selects-line behavior applied to every TextBox in the
    // app via styles (URL boxes, filename templates, multi-line ID lists). The mouse handling itself needs a shown window and is not covered.
    // TextBox must be created on an STA thread, hence StaThread.Run.
    public class TextBoxTripleClickBehaviorTests
    {
        private static (int start, int length) LineAt(string text, int caretIndex)
        {
            (int, int) result = default;
            StaThread.Run(() =>
            {
                var textBox = new TextBox { Text = text, CaretIndex = caretIndex };
                result = TextBoxTripleClickBehavior.GetCurrentLine(textBox);
            });
            return result;
        }

        // Verifies that in single-line text (the common case: URL and template boxes) triple-click selects the whole text.
        // Why: this is what users rely on to replace a pasted URL in one gesture.
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void SingleLine_SelectsEverything(int caret)
        {
            Assert.Equal((0, 11), LineAt("hello world", caret));
        }

        // Verifies that in multi-line text only the caret's line is selected, without the neighbouring line breaks.
        // Why: the mass-download URL list is one ID per line; copying a "line" that includes the previous line's newline pastes an extra blank line.
        [Fact(Skip = "Bug: GetCurrentLine returns the index of the preceding '\\n' as the line start, so the selection of any line after the first includes the previous line break (\"\\ncd\" instead of \"cd\")")]
        public void MultiLine_SelectsOnlyTheCaretLine()
        {
            Assert.Equal((3, 2), LineAt("ab\ncd\nef", 4));
        }

        // Verifies a caret at the very end of the text (after the last character) selects the last line instead of throwing.
        // Why: the handler runs inside a mouse event; an exception there surfaces as the app's "unexpected error" dialog.
        [Fact(Skip = "Bug: GetCurrentLine calls text.LastIndexOf('\\n', caretPos, caretPos), which throws ArgumentOutOfRangeException when the caret is at the end of non-empty text (caretPos == text.Length)")]
        public void CaretAtEnd_SelectsLastLine()
        {
            Assert.Equal((0, 5), LineAt("hello", 5));
        }

        // Verifies empty text yields an empty selection at 0.
        // Why: triple-clicking an empty box must not throw.
        [Fact]
        public void EmptyText_SelectsNothing()
        {
            Assert.Equal((0, 0), LineAt("", 0));
        }
    }
}
