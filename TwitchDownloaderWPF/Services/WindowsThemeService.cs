using Microsoft.Win32;
using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;

namespace TwitchDownloaderWPF.Services
{
    [SupportedOSPlatform("windows")]
    public class WindowsThemeService : ManagementEventWatcher
    {
        public event EventHandler<string> ThemeChanged;

        private const string REGISTRY_KEY_PATH = @"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string REGISTRY_KEY_NAME = "AppsUseLightTheme";
        private const string LIGHT_THEME = "Light";
        private const string DARK_THEME = "Dark";
        private const int WINDOWS_1809_BUILD_NUMBER = 17763;
        private const int WINDOWS_2004_INSIDER_BUILD_NUMBER = 18985;
        private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE_BEFORE_2004 = 19;
        private const int USE_IMMERSIVE_DARK_MODE_ATTRIBUTE = 20;

        public WindowsThemeService()
        {
            // If the OS is older than Windows 10 1809 then it doesn't have the app theme registry key
            if (Environment.OSVersion.Version.Major < 10 || Environment.OSVersion.Version.Build < WINDOWS_1809_BUILD_NUMBER)
                return;

            var currentUser = WindowsIdentity.GetCurrent().User;

            if (currentUser is null)
                return;

            var windowsQuery = $@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{currentUser.Value}\\{REGISTRY_KEY_PATH}' AND ValueName = '{REGISTRY_KEY_NAME}'";

            Query = new EventQuery(windowsQuery);
            EventArrived += WindowsThemeService_EventArrived;

            try
            {
                Start();
            }
            catch (ExternalException e)
            {
                MessageBox.Show(string.Format(Translations.Strings.UnableToStartWindowsThemeWatcher, $"0x{e.ErrorCode:x8}"), Translations.Strings.MessageBoxTitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WindowsThemeService_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var newWindowsTheme = GetWindowsTheme();
            Application.Current.Dispatcher.BeginInvoke(() => ThemeChanged?.Invoke(this, newWindowsTheme));
        }

        public static string GetWindowsTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH);
            if (key?.GetValue(REGISTRY_KEY_NAME) is not int windowsThemeValue)
            {
                return LIGHT_THEME;
            }

            return windowsThemeValue > 0
                ? LIGHT_THEME
                : DARK_THEME;
        }

        public static void SetTitleBarTheme(WindowCollection windows, bool useDarkTitleBar)
        {
            if (Environment.OSVersion.Version.Major < 10 || Environment.OSVersion.Version.Build < WINDOWS_1809_BUILD_NUMBER)
                return;

            var useDarkTitleBarInt = Convert.ToInt32(useDarkTitleBar);
            var darkTitleBarAttribute = Environment.OSVersion.Version.Build < WINDOWS_2004_INSIDER_BUILD_NUMBER
                ? USE_IMMERSIVE_DARK_MODE_ATTRIBUTE_BEFORE_2004
                : USE_IMMERSIVE_DARK_MODE_ATTRIBUTE;

            foreach (Window window in windows)
            {
                var windowHandle = new WindowInteropHelper(window).Handle;
                NativeFunctions.SetWindowAttribute(windowHandle, darkTitleBarAttribute, ref useDarkTitleBarInt, sizeof(int));
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
    }
}