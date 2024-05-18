using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Data;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using CheckComboBoxItem = HandyControl.Controls.CheckComboBoxItem;

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

        private void BtnTempBrowse_Click(object sender, RoutedEventArgs e)
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
            var currentCulture = Settings.Default.GuiCulture;
            foreach (var (culture, index) in AvailableCultures.All.Select((x, index) => (x, index)))
            {
                ComboLocale.Items.Add(culture.NativeName);
                if (culture.Code == currentCulture) ComboLocale.SelectedIndex = index;
            }

            ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.LogLevelVerbose, Tag = LogLevel.Verbose });
            ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.LogLevelInfo, Tag = LogLevel.Info });
            ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.LogLevelWarning, Tag = LogLevel.Warning });
            ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.LogLevelError, Tag = LogLevel.Error });
            // ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.LogLevelFfmpeg, Tag = LogLevel.Ffmpeg });
            var currentLogLevels = (LogLevel)Settings.Default.LogLevels;
            foreach (CheckComboBoxItem item in ComboLogLevels.Items)
            {
                if (currentLogLevels.HasFlag((Enum)item.Tag))
                {
                    ComboLogLevels.SelectedItems.Add(item);
                }
            }
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
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
            if (MessageBox.Show(Translations.Strings.ResetSettingsConfirmationMessage, Translations.Strings.ResetSettingsConfirmation, MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
                MessageBoxResult.Yes)
            {
                Settings.Default.Reset();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();

                // TODO: Don't require restarting the application to apply
                var commandLine = Environment.CommandLine;
                var arguments = Environment.GetCommandLineArgs();
                var fileName = arguments.FirstOrDefault(commandLine.StartsWith, "");

                if (fileName.EndsWith(".exe"))
                {
                    if (MessageBox.Show(Translations.Strings.TheApplicationMustBeRestartedMessage, string.Format(Translations.Strings.RestartTheApplication, nameof(TwitchDownloaderWPF)),
                            MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    {
                        // Create a cmd window that waits 2 seconds before restarting the application
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "cmd.exe",
                                Arguments = $"/C choice /C Y /N /D Y /T 2 & START \"\" \"{fileName}\"",
                                WindowStyle = ProcessWindowStyle.Hidden,
                                CreateNoWindow = true,
                                WorkingDirectory = Environment.CurrentDirectory
                            }
                        };

                        process.Start();
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    MessageBox.Show(Translations.Strings.TheApplicationMustBeRestartedMessage, string.Format(Translations.Strings.RestartTheApplication, nameof(TwitchDownloaderWPF)),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
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

        private void ComboLogLevels_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            var newLogLevel = ComboLogLevels.SelectedItems
                .Cast<CheckComboBoxItem>()
                .Sum(item => (int)item.Tag);
            Settings.Default.LogLevels = newLogLevel;
        }
    }
}
