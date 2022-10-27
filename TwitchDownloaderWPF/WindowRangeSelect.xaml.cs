using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloaderCore;

namespace TwitchDownloader
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
            if (CurrentRender.chatRoot.video != null)
            {
                startSeconds = (int)Math.Floor(CurrentRender.chatRoot.video.start);
                endSeconds = (int)Math.Ceiling(CurrentRender.chatRoot.video.end);
            }
            else
            {
                startSeconds = (int)Math.Floor(CurrentRender.chatRoot.comments.First().content_offset_seconds);
                endSeconds = (int)Math.Ceiling(CurrentRender.chatRoot.comments.Last().content_offset_seconds);
            }

            numStart.Text = startSeconds.ToString();
            numEnd.Text = endSeconds.ToString();
            rangeTime.Maximum = endSeconds;
            rangeTime.Minimum = startSeconds;
            rangeTime.ValueEnd = endSeconds;
        }

        private void rangeTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            numStart.Text = rangeTime.ValueStart.ToString();
            numEnd.Text = rangeTime.ValueEnd.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                startSeconds = int.Parse(numStart.Text);
                endSeconds = int.Parse(numEnd.Text);
                OK = true;
            }
            catch
            {
                Invalid = true;
            }
            this.Close();
        }

        private void numStart_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            
        }

        private void numEnd_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            
        }

        private void numStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                rangeTime.ValueStart = int.Parse(numStart.Text);
            }
            catch { }
        }

        private void numEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                rangeTime.ValueEnd = int.Parse(numEnd.Text);
            }
            catch { }
        }
    }
}
