using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloader.Properties;
using MessageBox = System.Windows.MessageBox;
using static TwitchDownloaderWPF.App;

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
            checkVerboseErrors.IsChecked = Settings.Default.VerboseErrors;

            comboTheme.Items.Add("System");
            string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");
            foreach (string themeFile in themeFiles)
            {
                comboTheme.Items.Add(Path.GetFileNameWithoutExtension(themeFile));
            }
            comboTheme.SelectedItem = Settings.Default.GuiTheme;
        }

        private void btnClearCache_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure you want to clear your cache?\nYou should only really do this if the program isn't working correctly", "Delete Confirmation", System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                //Let's clear the user selected temp folder and the default one
                string defaultDir = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
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

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			AppSingleton.RequestTitleBarChange();
		}

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.TemplateVod = textVodTemplate.Text;
            Settings.Default.TemplateClip = textClipTemplate.Text;
            Settings.Default.TemplateChat = textChatTemplate.Text;
            Settings.Default.TempPath = textTempPath.Text;
            Settings.Default.HideDonation = (bool)checkDonation.IsChecked;
            Settings.Default.VerboseErrors = (bool)checkVerboseErrors.IsChecked;
            Settings.Default.Save();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Uri uri = e.Uri;
            if (!uri.IsAbsoluteUri)
            {
                string destinationPath = Path.Combine(Environment.CurrentDirectory, uri.OriginalString);
                uri = new Uri(destinationPath);
            }

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

		private void comboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!comboTheme.SelectedItem.ToString().Equals(Settings.Default.GuiTheme, StringComparison.OrdinalIgnoreCase))
			{
				Settings.Default.GuiTheme = comboTheme.SelectedItem.ToString();
				AppSingleton.RequestAppThemeChange();
			}
		}
	}
}
