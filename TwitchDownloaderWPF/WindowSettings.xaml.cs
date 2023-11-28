﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Data;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class WindowSettings : Window
    {
        private bool _cancelSettingsChanges = true;
        private bool _refreshThemeOnCancel;
        private bool _refreshCultureOnCancel;

        public WindowSettings()
        {
            InitializeComponent();
        }

        private void btnTempBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                TextTempPath.Text = dialog.SelectedPath;
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (Settings.Default.TempPath == "")
            {
                TextTempPath.Text = Path.GetTempPath().TrimEnd('\\');
            }
            else
            {
                TextTempPath.Text = Settings.Default.TempPath;
            }

            TextVodTemplate.Text = Settings.Default.TemplateVod;
            TextClipTemplate.Text = Settings.Default.TemplateClip;
            TextChatTemplate.Text = Settings.Default.TemplateChat;
            CheckDonation.IsChecked = Settings.Default.HideDonation;
            CheckVerboseErrors.IsChecked = Settings.Default.VerboseErrors;
            NumMaximumBandwidth.Value = Settings.Default.MaximumBandwidthKib;
            NumMaximumBandwidth.IsEnabled = Settings.Default.DownloadThrottleEnabled;
            CheckThrottleEnabled.IsChecked = Settings.Default.DownloadThrottleEnabled;
            RadioTimeFormatUtc.IsChecked = Settings.Default.UTCVideoTime;

            if (Directory.Exists("Themes"))
            {
                // Setup theme dropdown
                ComboTheme.Items.Add("System"); // Cannot be localized
                string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");
                foreach (string themeFile in themeFiles)
                {
                    ComboTheme.Items.Add(Path.GetFileNameWithoutExtension(themeFile));
                }
                ComboTheme.SelectedItem = Settings.Default.GuiTheme;
            }

            // Setup culture dropdown
            foreach (var culture in AvailableCultures.All)
            {
                ComboLocale.Items.Add(culture.NativeName);
            }
            var currentCulture = Settings.Default.GuiCulture;
            var selectedIndex = AvailableCultures.All.Select((item, index) => new { item, index })
                .Where(x => x.item.Code == currentCulture)
                .Select(x => x.index)
                .DefaultIfEmpty(-1)
                .First();

            if (selectedIndex > -1)
            {
                ComboLocale.SelectedIndex = selectedIndex;
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
            App.RequestTitleBarChange();
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            if (_cancelSettingsChanges)
            {
                Settings.Default.Reload();
                if (_refreshThemeOnCancel)
                {
                    App.RequestAppThemeChange();
                }

                if (_refreshCultureOnCancel)
                {
                    App.RequestCultureChange();
                }
            }
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

        private void ComboTheme_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (!((string)ComboTheme.SelectedItem).Equals(Settings.Default.GuiTheme, StringComparison.OrdinalIgnoreCase))
            {
                _refreshThemeOnCancel = true;
                Settings.Default.GuiTheme = (string)ComboTheme.SelectedItem;
                App.RequestAppThemeChange();
            }
        }

        private void ComboLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            if (ComboLocale.SelectedIndex == -1)
                return;

            var selectedCulture = AvailableCultures.All[ComboLocale.SelectedIndex].Code;
            if (selectedCulture != Settings.Default.GuiCulture)
            {
                _refreshCultureOnCancel = true;
                Settings.Default.GuiCulture = selectedCulture;
                App.RequestCultureChange();
                Title = Translations.Strings.TitleGlobalSettings;
            }
        }

        private void CheckThrottleEnabled_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            NumMaximumBandwidth.IsEnabled = CheckThrottleEnabled.IsChecked.GetValueOrDefault();
            Settings.Default.DownloadThrottleEnabled = CheckThrottleEnabled.IsChecked.GetValueOrDefault();
        }

        private void NumMaximumBandwidth_OnValueChanged(object sender, FunctionEventArgs<double> e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.MaximumBandwidthKib = (int)NumMaximumBandwidth.Value;
        }

        private void BtnResetSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to reset all settings to their default values?", "Restore Settings Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Settings.Default.Reset();
                Settings.Default.Save();
                MessageBox.Show("The application must be restarted for changes to take effect.", $"Please restart {nameof(TwitchDownloaderWPF)}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSaveSettings_OnClick(object sender, RoutedEventArgs e)
        {
            _cancelSettingsChanges = false;
            Settings.Default.Save();
            Close();
        }

        private void BtnCancelSettings_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TextTempPath_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TempPath = TextTempPath.Text;
        }

        private void CheckDonation_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.HideDonation = CheckDonation.IsChecked.GetValueOrDefault();
        }

        private void RadioTimeFormat_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.UTCVideoTime = RadioTimeFormatUtc.IsChecked.GetValueOrDefault();
        }

        private void CheckVerboseErrors_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.VerboseErrors = CheckVerboseErrors.IsChecked.GetValueOrDefault();
        }

        private void TextVodTemplate_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TemplateVod = TextVodTemplate.Text;
        }

        private void TextClipTemplate_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TemplateClip = TextClipTemplate.Text;
        }

        private void TextChatTemplate_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.TemplateChat = TextChatTemplate.Text;
        }
    }
}
