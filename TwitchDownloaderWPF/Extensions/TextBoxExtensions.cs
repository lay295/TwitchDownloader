using System.Diagnostics.CodeAnalysis;
using System.Windows.Controls;

namespace TwitchDownloaderWPF.Extensions
{
    public static class TextBoxExtensions
    {
        public static bool TryInsertAtCaret([AllowNull] this TextBox textBox, string textToInsert)
        {
            if (textBox is null || string.IsNullOrEmpty(textToInsert))
            {
                return false;
            }

            var caretPos = textBox.CaretIndex;
            if (caretPos < 0)
            {
                return false;
            }

            if (textBox.IsSelectionActive)
            {
                textBox.Text = textBox.Text.Remove(caretPos, textBox.SelectionLength);
            }

            textBox.Text = textBox.Text.Insert(caretPos, textToInsert);
            textBox.CaretIndex = caretPos + textToInsert.Length;
            return true;
        }
    }
}