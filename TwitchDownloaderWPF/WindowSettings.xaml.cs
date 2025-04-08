using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using HandyControl.Data;
using TwitchDownloaderCore.Services;
using TwitchDownloaderWPF.Extensions;
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
            CheckReduceMotion.IsChecked = Settings.Default.ReduceMotion;

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

            var currentCollisionBehavior = (CollisionBehavior)Settings.Default.FileCollisionBehavior;
            for (var i = 0; i < ComboFileCollisionBehavior.Items.Count; i++)
            {
                var current = (ComboBoxItem)ComboFileCollisionBehavior.Items[i]!;
                if (currentCollisionBehavior == (CollisionBehavior)current.Tag)
                {
                    ComboFileCollisionBehavior.SelectedIndex = i;
                    break;
                }
            }
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            var messageBoxResult = MessageBox.Show(this, Translations.Strings.ClearCacheConfirmation.Replace(@"\n", Environment.NewLine), Translations.Strings.DeleteConfirmation, MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                //Let's clear the user selected temp folder and the default one
                CacheDirectoryService.ClearCacheDirectory(Settings.Default.TempPath, out _);
                CacheDirectoryService.ClearCacheDirectory(Path.GetTempPath(), out _);
            }
        }

        private void Window_OnSourceInitialized(object sender, EventArgs e)
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
                App.RequestAppThemeChange(true);
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
            if (MessageBox.Show(this, Translations.Strings.ResetSettingsConfirmationMessage, Translations.Strings.ResetSettingsConfirmation, MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
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
                    if (MessageBox.Show(this, Translations.Strings.TheApplicationMustBeRestartedMessage, string.Format(Translations.Strings.RestartTheApplication, nameof(TwitchDownloaderWPF)),
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
                    MessageBox.Show(this, Translations.Strings.TheApplicationMustBeRestartedMessage, string.Format(Translations.Strings.RestartTheApplication, nameof(TwitchDownloaderWPF)),
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

        private void ComboFileCollisionBehavior_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            var behavior = (ComboBoxItem)ComboFileCollisionBehavior.SelectedItem;
            Settings.Default.FileCollisionBehavior = (int)behavior.Tag;
        }

        private void FilenameParameter_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInitialized || sender is not Run { Text: var parameter })
                return;

            if (e.ChangedButton is not MouseButton.Left and not MouseButton.Middle)
                return;

            var focusedElement = Keyboard.FocusedElement;
            var textBox = GetFilenameTemplateTextBox(focusedElement);

            if (textBox is null)
                return;

            var oldCaretPos = textBox.CaretIndex;
            if (!textBox.TryInsertAtCaret(parameter))
                return;

            if (e.ChangedButton is MouseButton.Middle && oldCaretPos != -1)
            {
                // If we inserted a *_custom template, we can focus inside the quotation marks
                var quoteIndex = parameter.LastIndexOf('"');
                if (quoteIndex != -1)
                {
                    textBox.CaretIndex = oldCaretPos + quoteIndex;
                }
            }

            e.Handled = true;
        }

        [return: MaybeNull]
        private TextBox GetFilenameTemplateTextBox(IInputElement inputElement)
        {
            if (ReferenceEquals(inputElement, TextVodTemplate))
                return TextVodTemplate;

            if (ReferenceEquals(inputElement, TextClipTemplate))
                return TextClipTemplate;

            if (ReferenceEquals(inputElement, TextChatTemplate))
                return TextChatTemplate;

            return null;
        }

        private void CheckReduceMotion_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ReduceMotion = CheckReduceMotion.IsChecked.GetValueOrDefault();
        }
    }
}
