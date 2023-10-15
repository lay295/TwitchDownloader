using HandyControl.Data;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Serialization;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;

namespace TwitchDownloaderWPF.Services
{
    public class ThemeService
    {
        private const int WINDOWS_1809_BUILD_NUMBER = 17763;
        private const int WINDOWS_2004_INSIDER_BUILD_NUMBER = 18985;
        private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE_BEFORE_2004 = 19;
        private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE = 20;

        private bool _darkAppTitleBar = false;
        private bool _darkHandyControl = false;

        private readonly WindowsThemeService _windowsThemeService;
        private readonly App _wpfApplication;

        public ThemeService(App app, WindowsThemeService windowsThemeService)
        {
            if (!Directory.Exists("Themes"))
            {
                try
                {
                    Directory.CreateDirectory("Themes");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            if (!DefaultThemeService.WriteIncludedThemes())
            {
                MessageBox.Show(Translations.Strings.ThemesFailedToWrite, Translations.Strings.ThemesFailedToWrite, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _windowsThemeService = windowsThemeService;
            _wpfApplication = app;
            _windowsThemeService.ThemeChanged += WindowsThemeChanged;

            // If the current theme is not system and the old theme file is not found
            if (!Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine("Themes", $"{Settings.Default.GuiTheme}.xaml")))
            {
                MessageBox.Show(
                    Translations.Strings.ThemeNotFoundMessage.Replace("{theme}", $"{Settings.Default.GuiTheme}.xaml"),
                    Translations.Strings.ThemeNotFound,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Settings.Default.GuiTheme = "System";
                Settings.Default.Save();
            }
        }

        private void WindowsThemeChanged(object sender, string newWindowsTheme)
        {
            if (_wpfApplication.Windows.Count == 0)
                return;

            if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                ChangeAppTheme();
            }
        }

        public void ChangeAppTheme()
        {
            var newTheme = Settings.Default.GuiTheme;
            if (newTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                newTheme = WindowsThemeService.GetWindowsTheme();
            }

            ChangeThemePath(newTheme);

            var newSkin = _darkHandyControl ? SkinType.Dark : SkinType.Default;
            SetHandyControlTheme(newSkin);

            if (_wpfApplication.Windows.Count > 0)
            {
                SetTitleBarTheme(_wpfApplication.Windows);
            }
        }

        [SupportedOSPlatform("windows")]
        public void SetTitleBarTheme(WindowCollection windows)
        {
            if (Environment.OSVersion.Version.Major < 10 || Environment.OSVersion.Version.Build < WINDOWS_1809_BUILD_NUMBER)
                return;

            var shouldUseDarkTitleBar = Convert.ToInt32(_darkAppTitleBar);
            var darkTitleBarAttribute = Environment.OSVersion.Version.Build < WINDOWS_2004_INSIDER_BUILD_NUMBER
                ? USE_IMMERSIVE_DARK_MODE_ATTRIBUTE_BEFORE_2004
                : USE_IMMERSIVE_DARK_MODE_ATTRIBUTE;

            foreach (Window window in windows)
            {
                var windowHandle = new WindowInteropHelper(window).Handle;
                if (windowHandle == IntPtr.Zero)
                    continue;

                unsafe
                {
                    _ = NativeFunctions.SetWindowAttribute(windowHandle, darkTitleBarAttribute, &shouldUseDarkTitleBar, sizeof(int));
                }
            }

            Window wnd = new()
            {
                SizeToContent = SizeToContent.WidthAndHeight,
                Top = int.MinValue + 1,
                WindowStyle = WindowStyle.None
            };
            wnd.Show();
            wnd.Close();
            // Dark title bar is a bit buggy, requires window redraw (focus change, resize, transparency change) to fully apply.
            // We *could* send a repaint message to win32.dll, but this solution works and is way easier.
            // Win11 might not have this issue but Win10 does so please leave this
        }

        private void ChangeThemePath(string newTheme)
        {
            if (!Directory.Exists("Themes"))
                return;

            var themeFiles = Directory.GetFiles("Themes", "*.xaml");
            var newThemeString = Path.Combine("Themes", $"{newTheme}.xaml");

            foreach (var themeFile in themeFiles)
            {
                if (!newThemeString.Equals(themeFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var xmlReader = new XmlSerializer(typeof(ThemeResourceDictionaryModel));
                using var streamReader = new StreamReader(themeFile);
                var themeValues = (ThemeResourceDictionaryModel)xmlReader.Deserialize(streamReader)!;

                foreach (var solidBrush in themeValues.SolidColorBrush)
                {
                    try
                    {
                        _wpfApplication.Resources[solidBrush.Key] = (SolidColorBrush)new BrushConverter().ConvertFrom(solidBrush.Color);
                    }
                    catch (FormatException) { }
                }

                foreach (var boolean in themeValues.Boolean)
                {
                    switch (boolean.Key)
                    {
                        case "DarkTitleBar":
                            _darkAppTitleBar = boolean.Value;
                            break;
                        case "DarkHandyControl":
                            _darkHandyControl = boolean.Value;
                            break;
                    }
                }

                return;
            }
        }

        private void SetHandyControlTheme(SkinType newSkin)
        {
            _wpfApplication.Resources.MergedDictionaries[0].Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Skin{newSkin}.xaml", UriKind.Absolute);
            _wpfApplication.Resources.MergedDictionaries[1].Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute);
        }
    }
}