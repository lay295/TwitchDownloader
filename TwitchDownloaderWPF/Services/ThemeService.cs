using System;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Serialization;
using HandyControl.Data;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Translations;

namespace TwitchDownloaderWPF.Services;

public class ThemeService {
    private const int WINDOWS_1809_BUILD_NUMBER = 17763;
    private const int WINDOWS_2004_INSIDER_BUILD_NUMBER = 18985;
    private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE_BEFORE_2004 = 19;
    private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE = 20;

    private readonly WindowsThemeService _windowsThemeService;
    private readonly App _wpfApplication;

    private bool _darkAppTitleBar;
    private bool _darkHandyControl;

    public ThemeService(App app, WindowsThemeService windowsThemeService) {
        if (!Directory.Exists("Themes"))
            try {
                Directory.CreateDirectory("Themes");
            } catch (IOException) { } catch (UnauthorizedAccessException) { }

        if (!DefaultThemeService.WriteIncludedThemes())
            MessageBox.Show(
                Strings.ThemesFailedToWrite,
                Strings.ThemesFailedToWrite,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

        this._windowsThemeService = windowsThemeService;
        this._wpfApplication = app;
        this._windowsThemeService.ThemeChanged += this.WindowsThemeChanged;

        // If the current theme is not system and the old theme file is not found
        if (!Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(Path.Combine("Themes", $"{Settings.Default.GuiTheme}.xaml"))) {
            MessageBox.Show(
                Strings.ThemeNotFoundMessage.Replace("{theme}", $"{Settings.Default.GuiTheme}.xaml"),
                Strings.ThemeNotFound,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            Settings.Default.GuiTheme = "System";
            Settings.Default.Save();
        }
    }

    private void WindowsThemeChanged(object sender, string newWindowsTheme) {
        if (this._wpfApplication.Windows.Count == 0)
            return;

        if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            this.ChangeAppTheme(true);
    }

    public void ChangeAppTheme(bool forceRepaint = false) {
        var newTheme = Settings.Default.GuiTheme;
        if (newTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            newTheme = WindowsThemeService.GetWindowsTheme();

        this.ChangeThemePath(newTheme);

        var newSkin = this._darkHandyControl ? SkinType.Dark : SkinType.Default;
        this.SetHandyControlTheme(newSkin);

        if (this._wpfApplication.Windows.Count > 0)
            this.SetTitleBarTheme(this._wpfApplication.Windows);

        if (forceRepaint) {
            // Cause an NC repaint by changing focus
            var wnd = new Window {
                SizeToContent = SizeToContent.WidthAndHeight,
                Top = int.MinValue + 1,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false
            };
            wnd.Show();
            wnd.Close();
        }
    }

    [SupportedOSPlatform("windows")]
    public void SetTitleBarTheme(WindowCollection windows) {
        if (Environment.OSVersion.Version.Major < 10
            || Environment.OSVersion.Version.Build < ThemeService.WINDOWS_1809_BUILD_NUMBER)
            return;

        var shouldUseDarkTitleBar = Convert.ToInt32(this._darkAppTitleBar);
        var darkTitleBarAttribute = Environment.OSVersion.Version.Build < ThemeService.WINDOWS_2004_INSIDER_BUILD_NUMBER
            ? ThemeService.USE_IMMERSIVE_DARK_MODE_ATTRIBUTE_BEFORE_2004
            : ThemeService.USE_IMMERSIVE_DARK_MODE_ATTRIBUTE;

        foreach (Window window in windows) {
            var windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero)
                continue;

            unsafe {
                _ = NativeFunctions.SetWindowAttribute(
                    windowHandle,
                    darkTitleBarAttribute,
                    &shouldUseDarkTitleBar,
                    sizeof(int)
                );
            }
        }
    }

    private void ChangeThemePath(string newTheme) {
        if (!Directory.Exists("Themes"))
            return;

        var themeFiles = Directory.GetFiles("Themes", "*.xaml");
        var newThemeString = Path.Combine("Themes", $"{newTheme}.xaml");

        foreach (var themeFile in themeFiles) {
            if (!newThemeString.Equals(themeFile, StringComparison.OrdinalIgnoreCase))
                continue;

            var xmlReader = new XmlSerializer(typeof(ThemeResourceDictionaryModel));
            using var streamReader = new StreamReader(themeFile);
            var themeValues = (ThemeResourceDictionaryModel)xmlReader.Deserialize(streamReader)!;
            var brushConverter = new BrushConverter();

            foreach (var solidBrush in themeValues.SolidColorBrush)
                try {
                    this._wpfApplication.Resources[solidBrush.Key]
                        = (SolidColorBrush)brushConverter.ConvertFrom(solidBrush.Color);
                } catch (FormatException) { }

            foreach (var boolean in themeValues.Boolean)
                switch (boolean.Key) {
                    case "DarkTitleBar":
                        this._darkAppTitleBar = boolean.Value;
                        break;

                    case "DarkHandyControl":
                        this._darkHandyControl = boolean.Value;
                        break;
                }

            return;
        }
    }

    private void SetHandyControlTheme(SkinType newSkin) {
        this._wpfApplication.Resources.MergedDictionaries[0].Source = new(
            $"pack://application:,,,/HandyControl;component/Themes/Skin{newSkin}.xaml",
            UriKind.Absolute
        );
        this._wpfApplication.Resources.MergedDictionaries[1].Source = new(
            "pack://application:,,,/HandyControl;component/Themes/Theme.xaml",
            UriKind.Absolute
        );
    }
}
