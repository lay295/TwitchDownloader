using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Data;
using TwitchDownloaderCore;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for WindowRangeSelect.xaml
/// </summary>
public partial class WindowRangeSelect : Window {
    public WindowRangeSelect(ChatRenderer currentRender) {
        this.CurrentRender = currentRender;
        this.InitializeComponent();
    }

    public ChatRenderer CurrentRender { get; set; }
    public bool OK { get; set; }
    public bool Invalid { get; set; }
    public int startSeconds { get; set; }
    public int endSeconds { get; set; }

    private void Window_Initialized(object sender, EventArgs e) {
        this.startSeconds = (int)Math.Floor(this.CurrentRender.chatRoot.video.start);
        this.endSeconds = (int)Math.Ceiling(this.CurrentRender.chatRoot.video.end);

        this.numStart.Text = this.startSeconds.ToString(CultureInfo.CurrentCulture);
        this.numEnd.Text = this.endSeconds.ToString(CultureInfo.CurrentCulture);
        this.rangeTime.Maximum = this.endSeconds;
        this.rangeTime.Minimum = this.startSeconds;
        this.rangeTime.ValueEnd = this.endSeconds;
    }

    private void rangeTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<DoubleRange> e) {
        this.numStart.Text = this.rangeTime.ValueStart.ToString(CultureInfo.CurrentCulture);
        this.numEnd.Text = this.rangeTime.ValueEnd.ToString(CultureInfo.CurrentCulture);
    }

    private void Button_Click(object sender, RoutedEventArgs e) {
        try {
            this.startSeconds = int.Parse(this.numStart.Text, CultureInfo.CurrentCulture);
            this.endSeconds = int.Parse(this.numEnd.Text, CultureInfo.CurrentCulture);
            this.OK = true;
        } catch {
            this.Invalid = true;
        }

        this.Close();
    }

    private void numStart_TextChanged(object sender, TextChangedEventArgs e) {
        try {
            this.rangeTime.ValueStart = int.Parse(this.numStart.Text, CultureInfo.CurrentCulture);
        } catch (FormatException) { } catch (OverflowException) { }
    }

    private void numEnd_TextChanged(object sender, TextChangedEventArgs e) {
        try {
            this.rangeTime.ValueEnd = int.Parse(this.numEnd.Text, CultureInfo.CurrentCulture);
        } catch (FormatException) { } catch (OverflowException) { }
    }

    private void Window_OnSourceInitialized(object sender, EventArgs e) { App.RequestTitleBarChange(); }
}
