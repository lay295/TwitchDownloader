using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloaderCore;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for WindowRangeSelect.xaml
    /// </summary>
    public partial class WindowRangeSelect : Window
    {
        public ChatRenderer CurrentRender { get; set; }
        public bool OK { get; set; } = false;
        public bool Invalid { get; set; } = false;
        public int startSeconds { get; set; }
        public int endSeconds { get; set; }
        public WindowRangeSelect(ChatRenderer currentRender)
        {
            CurrentRender = currentRender;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            startSeconds = (int)Math.Floor(CurrentRender.chatRoot.video.start);
            endSeconds = (int)Math.Ceiling(CurrentRender.chatRoot.video.end);

            numStart.Text = startSeconds.ToString(CultureInfo.CurrentCulture);
            numEnd.Text = endSeconds.ToString(CultureInfo.CurrentCulture);
            rangeTime.Maximum = endSeconds;
            rangeTime.Minimum = startSeconds;
            rangeTime.ValueEnd = endSeconds;
        }

        private void rangeTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            numStart.Text = rangeTime.ValueStart.ToString(CultureInfo.CurrentCulture);
            numEnd.Text = rangeTime.ValueEnd.ToString(CultureInfo.CurrentCulture);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                startSeconds = int.Parse(numStart.Text, CultureInfo.CurrentCulture);
                endSeconds = int.Parse(numEnd.Text, CultureInfo.CurrentCulture);
                OK = true;
            }
            catch
            {
                Invalid = true;
            }
            this.Close();
        }

        private void numStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                rangeTime.ValueStart = int.Parse(numStart.Text, CultureInfo.CurrentCulture);
            }
            catch (FormatException) { }
            catch (OverflowException) { }
        }

        private void numEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                rangeTime.ValueEnd = int.Parse(numEnd.Text, CultureInfo.CurrentCulture);
            }
            catch (FormatException) { }
            catch (OverflowException) { }
        }

        private void Window_OnSourceInitialized(object sender, EventArgs e)
        {
            App.RequestTitleBarChange();
        }
    }
}
