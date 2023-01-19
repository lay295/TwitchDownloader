using HandyControl.Data;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;

namespace TwitchDownloaderWPF.Services
{
    public partial class ThemeService
    {
        private const int TITLEBAR_THEME_ATTRIBUTE = 20;

        private bool AppDarkTitleBar = false;
        private bool AppElementDarkTheme = false;

        private readonly WindowsThemeService _windowsThemeService;
        private readonly App _wpfApplication;

        public ThemeService(App app, WindowsThemeService windowsThemeService)
        {
            if (!Directory.Exists("Themes"))
            {
                Directory.CreateDirectory("Themes");
            }
            DefaultThemeService.WriteIncludedThemes();

            _windowsThemeService = windowsThemeService;
            _wpfApplication = app;
            _windowsThemeService.ThemeChanged += WindowsThemeChanged;

            // If the current theme is not system and the old theme file is not found
            if (!Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine("Themes", $"{Settings.Default.GuiTheme}.xaml")))
            {
                MessageBox.Show(
                    Translations.Strings.ThemeNotFoundMessage.Replace("{theme}", Settings.Default.GuiTheme + ".xaml"),
                    Translations.Strings.ThemeNotFound,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Settings.Default.GuiTheme = "System";
                Settings.Default.Save();
            }
        }

        private void WindowsThemeChanged(object sender, string newWindowsTheme)
        {
            if (_wpfApplication.Windows.Count > 0)
            {
                if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    ChangeAppTheme();
                }
            }
        }

        public void ChangeAppTheme()
        {
            string newTheme = Settings.Default.GuiTheme;
            if (newTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                newTheme = WindowsThemeService.GetWindowsTheme();
            }
            ChangeThemePath(_wpfApplication, newTheme);

            SkinType newSkin = AppElementDarkTheme ? SkinType.Dark : SkinType.Default;
            SetHandyControlTheme(newSkin, _wpfApplication);

            if (_wpfApplication.Windows.Count > 0)
            {
                SetTitleBarTheme(_wpfApplication.Windows);
            }
        }

        public void SetTitleBarTheme(WindowCollection windows)
        {
            // If windows 10 build is before 1903, it doesn't support dark title bars
            if (Environment.OSVersion.Version.Build < 18362)
            {
                return;
            }

            foreach (Window window in windows)
            {
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                NativeFunctions.SetWindowAttribute(windowHandle, TITLEBAR_THEME_ATTRIBUTE, ref AppDarkTitleBar, Marshal.SizeOf(AppDarkTitleBar));
            }

            Window _wnd = new()
            {
                SizeToContent = SizeToContent.WidthAndHeight
            };
            _wnd.Show();
            _wnd.Close();
            // Dark title bar is a bit buggy, requires window resize or focus change to fully apply
            // Win11 might not have this issue but Win10 does so please leave this
        }

        private void ChangeThemePath(App app, string newTheme)
        {
            string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");
            string newThemeString = $"{Path.Combine("Themes", newTheme)}.xaml";

            foreach (string themeFile in themeFiles)
            {
                if (newThemeString.Equals(themeFile, StringComparison.OrdinalIgnoreCase))
                {
                    var xmlReader = new XmlSerializer(typeof(ResourceDictionaryModel));
                    using var streamReader = new StreamReader(themeFile);
                    var themeValues = (ResourceDictionaryModel)xmlReader.Deserialize(streamReader);

                    foreach (SolidColorBrushModel solidBrush in themeValues.SolidColorBrush)
                    {
                        app.Resources[solidBrush.Key] = (SolidColorBrush)new BrushConverter().ConvertFrom(solidBrush.Color);
                    }
                    foreach (BooleanModel boolean in themeValues.Boolean)
                    {
                        switch (boolean.Key)
                        {
                            case "DarkTitleBar": AppDarkTitleBar = boolean.Value; break;
                            case "DarkModeElements": AppElementDarkTheme = boolean.Value; break;
                            default: break;
                        }
                    }
                    return;
                }
            }
        }

        private void SetHandyControlTheme(SkinType newSkin, App app)
        {
            app.Resources.MergedDictionaries[0].Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Skin{newSkin}.xaml", UriKind.Absolute);
            app.Resources.MergedDictionaries[1].Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute);
        }
    }
}
