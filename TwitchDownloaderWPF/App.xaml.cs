using System;
using System.IO;
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

            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Set the working dir to the process dir if run from sys32/syswow64
            var processDir = Directory.GetParent(Environment.ProcessPath!)!.FullName;
            if (Environment.CurrentDirectory != processDir)
            {
                Environment.CurrentDirectory = processDir;
            }

            RequestCultureChange();

            var windowsThemeService = new WindowsThemeService();
            ThemeServiceSingleton = new ThemeService(this, windowsThemeService);

            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var ex = e.Exception;
            MessageBox.Show(ex.ToString(), Strings.FatalError, MessageBoxButton.OK, MessageBoxImage.Error);

            Current?.Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
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
