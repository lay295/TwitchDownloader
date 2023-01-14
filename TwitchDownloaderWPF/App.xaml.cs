using System.Windows;
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
        public static App AppSingleton { get; private set; }

        public App()
        {
            AppSingleton = this;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set the current culture
            CultureService.SetApplicationCulture(Settings.Default.GuiCulture);

            // Setup theme service
            WindowsThemeService windowsThemeService = new();
            ThemeServiceSingleton = new ThemeService(this, windowsThemeService);

            // Create and show the main window
            MainWindow wnd = new();
            wnd.Show();
        }

        public void RequestAppThemeChange()
            => ThemeServiceSingleton.ChangeAppTheme();

        public void RequestTitleBarChange()
            => ThemeServiceSingleton.SetTitleBarTheme(Windows);
    }
}
