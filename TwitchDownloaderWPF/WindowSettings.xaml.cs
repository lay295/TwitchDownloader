using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using HandyControl.Data;
using Ookii.Dialogs.Wpf;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using CheckComboBoxItem = HandyControl.Controls.CheckComboBoxItem;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for SettingsPage.xaml
/// </summary>
public partial class WindowSettings : Window {
    private bool _cancelSettingsChanges = true;
    private bool _refreshCultureOnCancel;
    private bool _refreshThemeOnCancel;

    public WindowSettings() { this.InitializeComponent(); }

    private void BtnTempBrowse_Click(object sender, RoutedEventArgs e) {
        var dialog = new VistaFolderBrowserDialog();
        if (dialog.ShowDialog(this).GetValueOrDefault())
            this.TextTempPath.Text = dialog.SelectedPath;
    }

    private void Window_Initialized(object sender, EventArgs e) {
        if (Settings.Default.TempPath == "")
            this.TextTempPath.Text = Path.GetTempPath().TrimEnd('\\');
        else
            this.TextTempPath.Text = Settings.Default.TempPath;

        this.TextVodTemplate.Text = Settings.Default.TemplateVod;
        this.TextClipTemplate.Text = Settings.Default.TemplateClip;
        this.TextChatTemplate.Text = Settings.Default.TemplateChat;
        this.CheckDonation.IsChecked = Settings.Default.HideDonation;
        this.CheckVerboseErrors.IsChecked = Settings.Default.VerboseErrors;
        this.NumMaximumBandwidth.Value = Settings.Default.MaximumBandwidthKib;
        this.NumMaximumBandwidth.IsEnabled = Settings.Default.DownloadThrottleEnabled;
        this.CheckThrottleEnabled.IsChecked = Settings.Default.DownloadThrottleEnabled;
        this.RadioTimeFormatUtc.IsChecked = Settings.Default.UTCVideoTime;

        if (Directory.Exists("Themes")) {
            // Setup theme dropdown
            this.ComboTheme.Items.Add("System"); // Cannot be localized
            var themeFiles = Directory.GetFiles("Themes", "*.xaml");
            foreach (var themeFile in themeFiles)
                this.ComboTheme.Items.Add(Path.GetFileNameWithoutExtension(themeFile));
            this.ComboTheme.SelectedItem = Settings.Default.GuiTheme;
        }

        // Setup culture dropdown
        var currentCulture = Settings.Default.GuiCulture;
        foreach (var (culture, index) in AvailableCultures.All.Select((x, index) => (x, index))) {
            this.ComboLocale.Items.Add(culture.NativeName);
            if (culture.Code == currentCulture)
                this.ComboLocale.SelectedIndex = index;
        }

        this.ComboLogLevels.Items.Add(
            new CheckComboBoxItem { Content = Strings.LogLevelVerbose, Tag = LogLevel.Verbose }
        );
        this.ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Strings.LogLevelInfo, Tag = LogLevel.Info });
        this.ComboLogLevels.Items.Add(
            new CheckComboBoxItem { Content = Strings.LogLevelWarning, Tag = LogLevel.Warning }
        );
        this.ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Strings.LogLevelError, Tag = LogLevel.Error });
        // ComboLogLevels.Items.Add(new CheckComboBoxItem { Content = Translations.Strings.LogLevelFfmpeg, Tag = LogLevel.Ffmpeg });
        var currentLogLevels = (LogLevel)Settings.Default.LogLevels;
        foreach (CheckComboBoxItem item in this.ComboLogLevels.Items)
            if (currentLogLevels.HasFlag((Enum)item.Tag))
                this.ComboLogLevels.SelectedItems.Add(item);
    }

    private void BtnClearCache_Click(object sender, RoutedEventArgs e) {
        var messageBoxResult = MessageBox.Show(
            this,
            Strings.ClearCacheConfirmation.Replace(@"\n", Environment.NewLine),
            Strings.DeleteConfirmation,
            MessageBoxButton.YesNo
        );
        if (messageBoxResult == MessageBoxResult.Yes) {
            //Let's clear the user selected temp folder and the default one
            var defaultDir = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            var tempDir = Path.Combine(Settings.Default.TempPath, "TwitchDownloader");
            if (Directory.Exists(defaultDir))
                try {
                    Directory.Delete(defaultDir, true);
                } catch { }

            if (Directory.Exists(tempDir))
                try {
                    Directory.Delete(tempDir, true);
                } catch { }
        }
    }

    private void Window_OnSourceInitialized(object sender, EventArgs e) { App.RequestTitleBarChange(); }

    private void Window_Closing(object sender, EventArgs e) {
        if (this._cancelSettingsChanges) {
            Settings.Default.Reload();
            if (this._refreshThemeOnCancel)
                App.RequestAppThemeChange();

            if (this._refreshCultureOnCancel)
                App.RequestCultureChange();
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
        var uri = e.Uri;
        if (!uri.IsAbsoluteUri) {
            var destinationPath = Path.Combine(Environment.CurrentDirectory, uri.OriginalString);
            uri = new(destinationPath);
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ComboTheme_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        if (!((string)this.ComboTheme.SelectedItem).Equals(
                Settings.Default.GuiTheme,
                StringComparison.OrdinalIgnoreCase
            )) {
            this._refreshThemeOnCancel = true;
            Settings.Default.GuiTheme = (string)this.ComboTheme.SelectedItem;
            App.RequestAppThemeChange(true);
        }
    }

    private void ComboLocale_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        if (this.ComboLocale.SelectedIndex == -1)
            return;

        var selectedCulture = AvailableCultures.All[this.ComboLocale.SelectedIndex].Code;
        if (selectedCulture != Settings.Default.GuiCulture) {
            this._refreshCultureOnCancel = true;
            Settings.Default.GuiCulture = selectedCulture;
            App.RequestCultureChange();
        }
    }

    private void CheckThrottleEnabled_OnCheckedChanged(object sender, RoutedEventArgs e) {
        if (!this.IsInitialized)
            return;

        this.NumMaximumBandwidth.IsEnabled = this.CheckThrottleEnabled.IsChecked.GetValueOrDefault();
        Settings.Default.DownloadThrottleEnabled = this.CheckThrottleEnabled.IsChecked.GetValueOrDefault();
    }

    private void NumMaximumBandwidth_OnValueChanged(object sender, FunctionEventArgs<double> e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.MaximumBandwidthKib = (int)this.NumMaximumBandwidth.Value;
    }

    private void BtnResetSettings_OnClick(object sender, RoutedEventArgs e) {
        if (MessageBox.Show(
                this,
                Strings.ResetSettingsConfirmationMessage,
                Strings.ResetSettingsConfirmation,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            )
            == MessageBoxResult.Yes) {
            Settings.Default.Reset();
            Settings.Default.UpgradeRequired = false;
            Settings.Default.Save();

            // TODO: Don't require restarting the application to apply
            var commandLine = Environment.CommandLine;
            var arguments = Environment.GetCommandLineArgs();
            var fileName = arguments.FirstOrDefault(commandLine.StartsWith, "");

            if (fileName.EndsWith(".exe")) {
                if (MessageBox.Show(
                        this,
                        Strings.TheApplicationMustBeRestartedMessage,
                        string.Format(Strings.RestartTheApplication, nameof(TwitchDownloaderWPF)),
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Information
                    )
                    == MessageBoxResult.OK) {
                    // Create a cmd window that waits 2 seconds before restarting the application
                    var process = new Process {
                        StartInfo = new() {
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
            } else
                MessageBox.Show(
                    this,
                    Strings.TheApplicationMustBeRestartedMessage,
                    string.Format(Strings.RestartTheApplication, nameof(TwitchDownloaderWPF)),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
        }
    }

    private void BtnSaveSettings_OnClick(object sender, RoutedEventArgs e) {
        this._cancelSettingsChanges = false;
        Settings.Default.Save();
        this.Close();
    }

    private void BtnCancelSettings_OnClick(object sender, RoutedEventArgs e) { this.Close(); }

    private void TextTempPath_OnTextChanged(object sender, TextChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.TempPath = this.TextTempPath.Text;
    }

    private void CheckDonation_OnCheckedChanged(object sender, RoutedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.HideDonation = this.CheckDonation.IsChecked.GetValueOrDefault();
    }

    private void RadioTimeFormat_OnCheckedChanged(object sender, RoutedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.UTCVideoTime = this.RadioTimeFormatUtc.IsChecked.GetValueOrDefault();
    }

    private void CheckVerboseErrors_OnCheckedChanged(object sender, RoutedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.VerboseErrors = this.CheckVerboseErrors.IsChecked.GetValueOrDefault();
    }

    private void TextVodTemplate_OnTextChanged(object sender, TextChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.TemplateVod = this.TextVodTemplate.Text;
    }

    private void TextClipTemplate_OnTextChanged(object sender, TextChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.TemplateClip = this.TextClipTemplate.Text;
    }

    private void TextChatTemplate_OnTextChanged(object sender, TextChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        Settings.Default.TemplateChat = this.TextChatTemplate.Text;
    }

    private void ComboLogLevels_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!this.IsInitialized)
            return;

        var newLogLevel = this
            .ComboLogLevels.SelectedItems
            .Cast<CheckComboBoxItem>()
            .Sum(item => (int)item.Tag);
        Settings.Default.LogLevels = newLogLevel;
    }
}
