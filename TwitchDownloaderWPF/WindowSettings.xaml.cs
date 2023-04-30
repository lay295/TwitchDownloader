using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using static TwitchDownloaderWPF.App;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class WindowSettings : Window
    {
        public WindowSettings()
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
            NumMaximumBandwidth.Value = Settings.Default.MaximumBandwidthKib;
            NumMaximumBandwidth.IsEnabled = Settings.Default.DownloadThrottleEnabled;
            CheckThrottleEnabled.IsChecked = Settings.Default.DownloadThrottleEnabled;
            radioTimeFormatUTC.IsChecked = Settings.Default.UTCVideoTime;

            // Setup theme dropdown
            comboTheme.Items.Add("System"); // Cannot be localized
            string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");
            foreach (string themeFile in themeFiles)
            {
                comboTheme.Items.Add(Path.GetFileNameWithoutExtension(themeFile));
            }
            comboTheme.SelectedItem = Settings.Default.GuiTheme;

            // Setup culture dropdown
            foreach (var culture in AvailableCultures.All)
            {
                comboLocale.Items.Add(culture.NativeName);
            }
            var currentCulture = Settings.Default.GuiCulture;
            var selectedIndex = AvailableCultures.All.Select((item, index) => new { item, index })
                .Where(x => x.item.Code == currentCulture)
                .Select(x => x.index)
                .DefaultIfEmpty(-1)
                .First();

            if (selectedIndex > -1)
            {
                comboLocale.SelectedIndex = selectedIndex;
            }
        }

        private void btnClearCache_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show(Translations.Strings.ClearCacheConfirmation.Replace(@"\n", Environment.NewLine), Translations.Strings.DeleteConfirmation, MessageBoxButton.YesNo);
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
            Title = Translations.Strings.TitleGlobalSettings;
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
            Settings.Default.MaximumBandwidthKib = (int)NumMaximumBandwidth.Value;
            Settings.Default.UTCVideoTime = (bool)radioTimeFormatUTC.IsChecked;
            Settings.Default.DownloadThrottleEnabled = (bool)CheckThrottleEnabled.IsChecked;
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
            if (!((string)comboTheme.SelectedItem).Equals(Settings.Default.GuiTheme, StringComparison.OrdinalIgnoreCase))
            {
                Settings.Default.GuiTheme = (string)comboTheme.SelectedItem;
                AppSingleton.RequestAppThemeChange();
            }
        }

        private void comboLocale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboLocale.SelectedIndex == -1)
            {
                return;
            }

            var selectedCulture = AvailableCultures.All[comboLocale.SelectedIndex].Code;
            if (selectedCulture != Settings.Default.GuiCulture)
            {
                Settings.Default.GuiCulture = selectedCulture;
                AppSingleton.RequestCultureChange();
                Title = Translations.Strings.TitleGlobalSettings;
            }
        }

        private void CheckThrottleEnabled_Checked(object sender, RoutedEventArgs e)
        {
            NumMaximumBandwidth.IsEnabled = true;
        }

        private void CheckThrottleEnabled_Unchecked(object sender, RoutedEventArgs e)
        {
            NumMaximumBandwidth.IsEnabled = false;
        }
    }
}
