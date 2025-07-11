using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ThemeService ThemeServiceSingleton { get; private set; }
        public static CultureService CultureServiceSingleton { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            UpgradeSettings();

            base.OnStartup(e);

            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Set the working dir to the app dir in case we inherited a different working dir
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            CultureServiceSingleton = new CultureService();
            RequestCultureChange();

            var windowsThemeService = new WindowsThemeService();
            ThemeServiceSingleton = new ThemeService(this, windowsThemeService);

            MainWindow = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            MainWindow.Show();
        }

        private static void UpgradeSettings()
        {
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }
        }

        private static void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            ShowRecoverableExceptionMessage(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;

            if (e.IsTerminating)
            {
                MessageBox.Show(ex.ToString(), Translations.Strings.FatalError, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ShowRecoverableExceptionMessage(ex);
        }

        private static void ShowRecoverableExceptionMessage(Exception exception)
        {
            var message = exception + Environment.NewLine + Environment.NewLine + Environment.NewLine + string.Format(Translations.Strings.FatalErrorMessage, nameof(TwitchDownloaderWPF));
            var result = MessageBox.Show(message, Translations.Strings.FatalError, MessageBoxButton.OKCancel, MessageBoxImage.Error);

            if (result is MessageBoxResult.OK)
            {
                Current?.Shutdown();
            }
        }

        public static void RequestAppThemeChange(bool forceRepaint = false)
            => ThemeServiceSingleton.ChangeAppTheme(forceRepaint);

        public static void RequestTitleBarChange()
            => ThemeServiceSingleton.SetTitleBarTheme(Current.Windows);

        public static void RequestCultureChange()
            => CultureServiceSingleton.SetApplicationCulture(Settings.Default.GuiCulture);
    }
}
