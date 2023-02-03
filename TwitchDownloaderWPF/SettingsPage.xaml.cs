using System;
using System.Collections.Generic;
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
    public partial class SettingsPage : Window
    {
        private readonly List<(string name, string nativeName)> _cultureList = new();

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

            // Setup theme dropdown
            comboTheme.Items.Add("System"); // Cannot be localized
            string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");
            foreach (string themeFile in themeFiles)
            {
                comboTheme.Items.Add(Path.GetFileNameWithoutExtension(themeFile));
            }
            comboTheme.SelectedItem = Settings.Default.GuiTheme;

            // Setup culture dropdown
            foreach (var culture in (AvailableCultures.Culture[])Enum.GetValues(typeof(AvailableCultures.Culture)))
            {
                string name = culture.ToName();
                string nativeName = culture.ToNativeName();
                _cultureList.Add((name, nativeName));
                comboLocale.Items.Add(nativeName);
            }
            var currentCulture = Settings.Default.GuiCulture;
            var selectedIndex = _cultureList.Select((item, index) => new { item, index })
                .Where(x => x.item.name == currentCulture)
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

        private void comboLocale_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboLocale.SelectedIndex == -1)
            {
                return;
            }

            if (_cultureList[comboLocale.SelectedIndex].name != Settings.Default.GuiCulture)
            {
                Settings.Default.GuiCulture = _cultureList[comboLocale.SelectedIndex].name;
                AppSingleton.RequestCultureChange();
            }
        }
    }
}
