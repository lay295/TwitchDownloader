using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TwitchDownloader.Properties;

namespace TwitchDownloaderWPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public static ThemeHelper themeHelper;

		private void Application_Startup(object sender, StartupEventArgs e)
		{
			themeHelper = new ThemeHelper(this);
			themeHelper.WatchTheme();

			MainWindow wnd = new();
			wnd.Show();
		}

		public void RequestAppThemeChange()
		{
			themeHelper.ChangeAppTheme();
		}
	}
}
