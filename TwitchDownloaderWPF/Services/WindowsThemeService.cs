using Microsoft.Win32;
using System;
using System.Management;
using System.Security.Principal;

namespace TwitchDownloader.Tools
{
    public class WindowsThemeService : ManagementEventWatcher
    {
        public event EventHandler<string> ThemeChanged;

        private const string REGISTRY_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string REGISTRY_KEY_NAME = "AppsUseLightTheme";
        internal const string LIGHT_THEME = "Light";
        internal const string DARK_THEME = "Dark";

        public WindowsThemeService() : base()
        {
            var currentUser = WindowsIdentity.GetCurrent();
            var windowsQuery = $"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = " +
                $"'{currentUser.User.Value}\\{REGISTRY_KEY_PATH}' AND ValueName = '{REGISTRY_KEY_NAME}'";
            windowsQuery = windowsQuery.Replace("\\", @"\\");

            Query = new EventQuery(windowsQuery);
            EventArrived += WindowsThemeService_EventArrived;
        }

        private void WindowsThemeService_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var newWindowsTheme = GetWindowsTheme();
            if (ThemeChanged.GetInvocationList().Length > 0)
            {
                ThemeChanged.Invoke(this, newWindowsTheme);
            }
        }

        public static string GetWindowsTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH);
                if (!(key.GetValue(REGISTRY_KEY_NAME) is int windowsThemeValue))
                {
                    return LIGHT_THEME;
                }

                return windowsThemeValue > 0
                    ? LIGHT_THEME
                    : DARK_THEME;
            }
            catch (NullReferenceException) // Usually means key does not exist
            {
                return LIGHT_THEME;
            }
        }
    }
}
