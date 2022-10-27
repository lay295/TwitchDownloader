using System.Windows;
using TwitchDownloader;

namespace TwitchDownloaderWPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public static ThemeHelper themeHelper;
		public static App AppSingleton;

		public App()
		{
			AppSingleton = this;
		}

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			themeHelper = new ThemeHelper();
			themeHelper.WatchTheme(this);

			MainWindow wnd = new();
			wnd.Show();
		}

		public void RequestAppThemeChange()
		{
			themeHelper.ChangeAppTheme(this);
		}
	}
}
