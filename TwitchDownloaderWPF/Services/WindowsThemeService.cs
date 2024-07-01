using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Windows;
using Microsoft.Win32;
using TwitchDownloaderWPF.Translations;

namespace TwitchDownloaderWPF.Services;

[SupportedOSPlatform("windows")]
public class WindowsThemeService : ManagementEventWatcher {

    private const string REGISTRY_KEY_PATH = @"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
    private const string REGISTRY_KEY_NAME = "AppsUseLightTheme";
    private const string LIGHT_THEME = "Light";
    private const string DARK_THEME = "Dark";
    private const int WINDOWS_1809_BUILD_NUMBER = 17763;

    public WindowsThemeService() {
        // If the OS is older than Windows 10 1809 then it doesn't have the app theme registry key
        if (Environment.OSVersion.Version.Major < 10
            || Environment.OSVersion.Version.Build < WindowsThemeService.WINDOWS_1809_BUILD_NUMBER)
            return;

        var currentUser = WindowsIdentity.GetCurrent().User;

        if (currentUser is null)
            return;

        var windowsQuery
            = $@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{currentUser.Value}\\{WindowsThemeService.REGISTRY_KEY_PATH}' AND ValueName = '{WindowsThemeService.REGISTRY_KEY_NAME}'";

        this.Query = new(windowsQuery);
        this.EventArrived += this.WindowsThemeService_EventArrived;

        try {
            this.Start();
        } catch (ExternalException e) {
            MessageBox.Show(
                string.Format(Strings.UnableToStartWindowsThemeWatcher, $"0x{e.ErrorCode:x8}"),
                Strings.MessageBoxTitleError,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        } catch (ManagementException e) {
            MessageBox.Show(
                string.Format(Strings.UnableToStartWindowsThemeWatcher, $"{e.ErrorCode} (0x{(int)e.ErrorCode:x8})"),
                Strings.MessageBoxTitleError,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    public event EventHandler<string> ThemeChanged;

    private void WindowsThemeService_EventArrived(object sender, EventArrivedEventArgs e) {
        var newWindowsTheme = GetWindowsTheme();
        Application.Current.Dispatcher.BeginInvoke(() => this.ThemeChanged?.Invoke(this, newWindowsTheme));
    }

    public static string GetWindowsTheme() {
        using var key = Registry.CurrentUser.OpenSubKey(WindowsThemeService.REGISTRY_KEY_PATH);
        if (key?.GetValue(WindowsThemeService.REGISTRY_KEY_NAME) is not int windowsThemeValue)
            return WindowsThemeService.LIGHT_THEME;

        return windowsThemeValue > 0
            ? WindowsThemeService.LIGHT_THEME
            : WindowsThemeService.DARK_THEME;
    }
}
