using HandyControl.Data;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;

namespace TwitchDownloaderWPF.Services
{
    public class ThemeService
    {
        private bool _darkAppTitleBar = false;
        private bool _darkHandyControl = false;

        private readonly WindowsThemeService _windowsThemeService;
        private readonly App _wpfApplication;

        public ThemeService(App app, WindowsThemeService windowsThemeService)
        {
            if (!Directory.Exists("Themes"))
            {
                Directory.CreateDirectory("Themes");
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

        public void SetTitleBarTheme(WindowCollection windows) => WindowsThemeService.SetTitleBarTheme(windows, _darkAppTitleBar);

        private void ChangeThemePath(string newTheme)
        {
            var themeFiles = Directory.GetFiles("Themes", "*.xaml");
            var newThemeString = Path.Combine("Themes", $"{newTheme}.xaml");

            foreach (var themeFile in themeFiles)
            {
                if (!newThemeString.Equals(themeFile, StringComparison.OrdinalIgnoreCase))
                    continue;

                var xmlReader = new XmlSerializer(typeof(ResourceDictionaryModel));
                using var streamReader = new StreamReader(themeFile);
                var themeValues = (ResourceDictionaryModel)xmlReader.Deserialize(streamReader)!;

                foreach (var solidBrush in themeValues.SolidColorBrush)
                {
                    _wpfApplication.Resources[solidBrush.Key] = (SolidColorBrush)new BrushConverter().ConvertFrom(solidBrush.Color);
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