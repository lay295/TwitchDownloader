using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TwitchDownloader.Properties;

namespace TwitchDownloader
{
    /// <summary>
    /// Interaction logic for FfmpegOptions.xaml
    /// </summary>
    public partial class FfmpegOptions : Window
    {
        public FfmpegOptions()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadSettings();
            imageWarning.Source = Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Warning.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private void LoadSettings()
        {
            textInputArgs.Text = Settings.Default.FfmpegInputArgs;
            textOutputArgs.Text = Settings.Default.FfmpegOutputArgs;
        }

        private void SaveSettings()
        {
             Settings.Default.FfmpegInputArgs = textInputArgs.Text;
             Settings.Default.FfmpegOutputArgs = textOutputArgs.Text;
             Settings.Default.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            textInputArgs.Text = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -";
            textOutputArgs.Text = "-c:v libx264 -preset medium -pix_fmt yuv420p \"{save_path}\"";
        }
    }
}
