using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace TwitchDownloaderWPF
{
    public partial class WindowToast : Window
    {
        private readonly DispatcherTimer _timer;
        private static int _activeToastCount = 0;
        private readonly int _toastIndex;

        public WindowToast(string title, string message, bool isError)
        {
            InitializeComponent();

            TextTitle.Text = title;
            TextMessage.Text = message;
            ColorBar.Background = isError
                ? new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));

            _toastIndex = _activeToastCount++;

            PositionWindow();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                _activeToastCount = Math.Max(0, _activeToastCount - 1);
                Close();
            };
            _timer.Start();
        }

        private void PositionWindow()
        {
            const int margin = 16;
            const int toastHeight = 70;
            const int toastSpacing = 8;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            Left = screenWidth - Width - margin;
            Top = screenHeight - (toastHeight + toastSpacing) * (_toastIndex + 1) - margin;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _timer.Stop();
            _activeToastCount = Math.Max(0, _activeToastCount - 1);
            Close();
        }
    }
}
