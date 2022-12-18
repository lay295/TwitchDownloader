using System.Windows;
using TwitchDownloader.Tools;

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
            WindowsThemeService windowsThemeService = new();

            ThemeServiceSingleton = new ThemeService(this, windowsThemeService);

            MainWindow wnd = new();
            wnd.Show();
        }

        public void RequestAppThemeChange()
            => ThemeServiceSingleton.ChangeAppTheme();

        public void RequestTitleBarChange()
            => ThemeServiceSingleton.SetTitleBarTheme(Windows);
    }
}
