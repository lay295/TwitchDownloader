using Microsoft.Win32;
using System;
using System.Management;
using System.Security.Principal;

namespace TwitchDownloaderWPF.Services
{
    public class WindowsThemeService : ManagementEventWatcher
    {
        public event EventHandler<string> ThemeChanged;

        private const string REGISTRY_KEY_PATH = @"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
        private const string REGISTRY_KEY_NAME = "AppsUseLightTheme";
        private const string LIGHT_THEME = "Light";
        private const string DARK_THEME = "Dark";

        public WindowsThemeService()
        {
            var currentUser = WindowsIdentity.GetCurrent().User;

            if (currentUser is null)
                return;

            var windowsQuery = $@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{currentUser.Value}\\{REGISTRY_KEY_PATH}' AND ValueName = '{REGISTRY_KEY_NAME}'";

            Query = new EventQuery(windowsQuery);
            EventArrived += WindowsThemeService_EventArrived;

            Start();
        }

        private void WindowsThemeService_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var newWindowsTheme = GetWindowsTheme();
            App.AppSingleton.Dispatcher.BeginInvoke(() => ThemeChanged?.Invoke(this, newWindowsTheme));
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
    }
}