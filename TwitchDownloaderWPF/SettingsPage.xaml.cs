using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwitchDownloader.Properties;

namespace TwitchDownloader
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Window
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        private void btnTempBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                textTempPath.Text = dialog.SelectedPath;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (Settings.Default.TempPath == "")
            {
                textTempPath.Text = Path.GetTempPath().TrimEnd('\\');
            }
            else
            {
                textTempPath.Text = Settings.Default.TempPath;
            }

            textVodTemplate.Text = Settings.Default.TemplateVod;
            textClipTemplate.Text = Settings.Default.TemplateClip;
            textChatTemplate.Text = Settings.Default.TemplateChat;
            checkDonation.IsChecked = Settings.Default.HideDonation;
        }

        private void btnClearCache_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Are you sure you want to clear your cache?\nYou should only really do this if the program isn't working correctly", "Delete Confirmation", System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                //Let's clear the user selected temp folder and the default one
                string defaultDir = Path.Combine(System.IO.Path.GetTempPath(), "TwitchDownloader");
                string tempDir = Path.Combine(Settings.Default.TempPath, "TwitchDownloader");
                if (Directory.Exists(defaultDir))
                {
                    try
                    {
                        Directory.Delete(defaultDir, true);
                    }
                    catch { }
                }
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch { }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.TemplateVod = textVodTemplate.Text;
            Settings.Default.TemplateClip = textClipTemplate.Text;
            Settings.Default.TemplateChat = textChatTemplate.Text;
            Settings.Default.TempPath = textTempPath.Text;
            Settings.Default.HideDonation = (bool)checkDonation.IsChecked;
            Settings.Default.Save();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
