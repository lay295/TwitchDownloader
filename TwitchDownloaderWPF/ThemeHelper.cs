using HandyControl.Data;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using TwitchDownloader.Models;
using TwitchDownloader.Properties;
using TwitchDownloaderWPF;

namespace TwitchDownloader
{
    public partial class ThemeHelper
    {
        private bool AppComponentsDarkTheme = false;

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", PreserveSig = true)]
        private static extern int SetWindowAttribute(IntPtr handle, int attribute, ref bool attributeValue, int attributeSize);
        private const int THEME_ATTRIBUTE = 20;

        private const string REGISTRY_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string REGISTRY_KEY_NAME = "AppsUseLightTheme";
        private const string WINDOWS_LIGHT_THEME = "Light";
        private const string WINDOWS_DARK_THEME = "Dark";

        public ThemeHelper()
        {
            if (!Directory.Exists("Themes"))
            {
                Directory.CreateDirectory("Themes");
            }
            WriteDefaultThemes();

            if (!Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase) && !File.Exists($"{Path.Combine("Themes", Settings.Default.GuiTheme)}.xaml"))
            {
                MessageBox.Show($"{Settings.Default.GuiTheme}.xaml was not found. Reverting theme to System", "Theme not found", MessageBoxButton.OK, MessageBoxImage.Information);
                Settings.Default.GuiTheme = "System";
            }
        }

        public void SetTitleBarThemes(WindowCollection windows)
        {
            try
            {
                foreach (Window window in windows)
                {
                    var windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;

                    SetWindowAttribute(windowHandle, THEME_ATTRIBUTE, ref AppComponentsDarkTheme, Marshal.SizeOf(AppComponentsDarkTheme));
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
            catch { }
        }

        public void WatchTheme(App app)
        {
            var currentUser = WindowsIdentity.GetCurrent();
            string windowsQuery = $"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = " +
                $"'{currentUser.User.Value}\\{REGISTRY_KEY_PATH}' AND ValueName = '{REGISTRY_KEY_NAME}'";
            windowsQuery = windowsQuery.Replace("\\", @"\\");

            try
            {
                var watcher = new ManagementEventWatcher(windowsQuery);
                watcher.EventArrived += (sender, args) =>
                {
                    if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
                    {
                        app.Dispatcher.Invoke(new Action(() => ChangeAppTheme(app)));
                    }
                };

                watcher.Start();
            }
            catch (PlatformNotSupportedException)
            {
                Settings.Default.GuiTheme = WINDOWS_LIGHT_THEME;
                MessageBox.Show("Unable to fetch Windows theme. System theming is now disabled.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.StackTrace, ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ChangeAppTheme(app);
        }

        public void ChangeAppTheme(App app)
        {
            string newTheme = Settings.Default.GuiTheme;
            if (newTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                newTheme = GetWindowsTheme();
            }
            ChangeThemePath(newTheme, app);

            AppComponentsDarkTheme = DetermineDarkMode(app);

            SkinType newSkin = AppComponentsDarkTheme ? SkinType.Dark : SkinType.Default;
            WriteHandyControlTheme(newSkin, app);

            if (app.Windows.Count > 0)
            {
                SetTitleBarThemes(app.Windows);
            }
        }

        private void ChangeThemePath(string newTheme, App app)
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
                    return;
                }
            }
        }

        private bool DetermineDarkMode(App app)
        {
            var appBackgroundColor = (Color)new ColorConverter().ConvertFrom(app.Resources["AppBackground"].ToString());
            var r = appBackgroundColor.R / 255f;
            var g = appBackgroundColor.G / 255f;
            var b = appBackgroundColor.B / 255f;
            float luminance = 0.5f * (Math.Min(Math.Min(r, g), b) + Math.Max(Math.Max(r, g), b));
            if (luminance < 0.33f)
            {
                return true;
            }
            return false;
        }

        private void WriteHandyControlTheme(SkinType newSkin, App app)
        {
            app.Resources.MergedDictionaries[0].Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Skin{newSkin}.xaml", UriKind.Absolute);
            app.Resources.MergedDictionaries[1].Source = new Uri($"pack://application:,,,/HandyControl;component/Themes/Theme.xaml", UriKind.Absolute);
        }

        private void WriteDefaultThemes()
        {
            var resourceNames = GetResourceNames();
            var themePaths = resourceNames.Where((i) => i.StartsWith($"{nameof(TwitchDownloader)}.Themes."));

            foreach (var themePath in themePaths)
            {
                var themeData = ReadResource(themePath);
                var themePathSplit = themePath.Split(".");

                var themeName = themePathSplit[^2];
                var themeExtension = themePathSplit[^1];
                var themeFullName = $"{themeName}.{themeExtension}";

                File.WriteAllText(Path.Combine("Themes", themeFullName), themeData);
            }
        }

        private string[] GetResourceNames()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames();
        }

        private string ReadResource(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var manifestStream = assembly.GetManifestResourceStream(resourcePath);
            using var streamReader = new StreamReader(manifestStream);

            return streamReader.ReadToEnd();
        }

        private string GetWindowsTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH);
            if (!(key.GetValue(REGISTRY_KEY_NAME) is int windowsThemeValue))
            {
                return WINDOWS_LIGHT_THEME;
            }

            return windowsThemeValue > 0 ? WINDOWS_LIGHT_THEME : WINDOWS_DARK_THEME;
        }
    }
}
