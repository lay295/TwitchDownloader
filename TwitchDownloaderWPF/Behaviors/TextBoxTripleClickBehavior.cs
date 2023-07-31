using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TwitchDownloaderWPF.Behaviors
{
    public class TextBoxTripleClickBehavior : DependencyObject
    {
        public static readonly DependencyProperty TripleClickSelectLineProperty = DependencyProperty.RegisterAttached(
            nameof(TripleClickSelectLine), typeof(bool), typeof(TextBoxTripleClickBehavior), new PropertyMetadata(false, OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBox textBox)
            {
                return;
            }

            var enable = (bool)e.NewValue;
            if (enable)
            {
                textBox.PreviewMouseLeftButtonDown += OnTextBoxMouseDown;
            }
            else
            {
                textBox.PreviewMouseLeftButtonDown -= OnTextBoxMouseDown;
            }
        }

        private static void OnTextBoxMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 3 && sender is TextBox textBox)
            {
                var (start, length) = GetCurrentLine(textBox);
                textBox.Select(start, length);
            }
        }

        private static (int start, int length) GetCurrentLine(TextBox textBox)
        {
            var caretPos = textBox.CaretIndex;
            var text = textBox.Text;

            var start = text.LastIndexOf('\n', caretPos, caretPos);
            var end = text.IndexOf('\n', caretPos);

            if (start == -1)
            {
                start = 0;
            }

            if (end == -1)
            {
                end = text.Length;
            }

            return (start, end - start);
        }

        public bool TripleClickSelectLine
        {
            get => (bool)GetValue(TripleClickSelectLineProperty);
            set => SetValue(TripleClickSelectLineProperty, value);
        }
    }
}
