using System.Windows;
using System.Windows.Input;

namespace TwitchDownloaderWPF
{
    public partial class WindowInputText : Window
    {
        public string InputValue { get; private set; } = "";

        public WindowInputText(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            TextPrompt.Text = prompt;
            TextInputBox.Text = defaultValue;
            TextInputBox.SelectAll();
        }

        protected override void OnContentRendered(System.EventArgs e)
        {
            base.OnContentRendered(e);
            TextInputBox.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            InputValue = TextInputBox.Text.Trim();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TextInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputValue = TextInputBox.Text.Trim();
                DialogResult = true;
            }
        }
    }
}
