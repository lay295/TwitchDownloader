using System;
using System.Windows;
using System.Windows.Threading;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ThemeService ThemeServiceSingleton { get; private set; }
        public static App AppSingleton { get; private set; }

        public App()
        {
            AppSingleton = this;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set the current culture
            RequestCultureChange();

            // Setup theme service
            WindowsThemeService windowsThemeService = new();
            ThemeServiceSingleton = new ThemeService(this, windowsThemeService);

            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Create and show the main window
            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            MessageBox.Show(ex.ToString(), Strings.FatalError, MessageBoxButton.OK, MessageBoxImage.Error);

            Current?.Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            MessageBox.Show(ex.ToString(), Strings.FatalError, MessageBoxButton.OK, MessageBoxImage.Error);

            Current?.Shutdown();
        }

        public void RequestAppThemeChange()
            => ThemeServiceSingleton.ChangeAppTheme();

        public void RequestTitleBarChange()
            => ThemeServiceSingleton.SetTitleBarTheme(Windows);

        public void RequestCultureChange()
            => CultureService.SetApplicationCulture(Settings.Default.GuiCulture);
    }
}
