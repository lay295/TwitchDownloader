using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using TwitchDownloader.Properties;
using TwitchDownloader.Models;
using TwitchDownloaderWPF;

namespace TwitchDownloader
{
	public partial class ThemeHelper
	{
		private bool WindowsIsDarkTheme;

		[DllImport("dwmapi.dll", PreserveSig = true)]
		public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref bool attrValue, int attrSize);

		private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
		private const string RegistryValueName = "AppsUseLightTheme";

		public ThemeHelper()
		{
			if (!Directory.Exists("Themes"))
			{
				Directory.CreateDirectory("Themes");
			}
			WriteDefaultThemes();

			if (!Settings.Default.GuiTheme.Equals("System") && !File.Exists($"{Path.Combine("Themes", Settings.Default.GuiTheme)}.xaml"))
			{
				MessageBox.Show($"{Settings.Default.GuiTheme}.xaml was not found. Reverting theme to System", "Theme not found", MessageBoxButton.OK, MessageBoxImage.Information);
				Settings.Default.GuiTheme = "System";
			}
		}

		public void UpdateTitleBarTheme(Window wnd)
		{
			if (Settings.Default.GuiTheme.Contains("Dark", StringComparison.OrdinalIgnoreCase) || Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase) && WindowsIsDarkTheme)
			{
				bool isDarkTheme = true;
				DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(wnd).Handle, 20, ref isDarkTheme, Marshal.SizeOf(isDarkTheme));

				Window _wnd = new();
				_wnd.SizeToContent = SizeToContent.WidthAndHeight;
				_wnd.Show();
				_wnd.Close();
				// Dark title bar is a bit buggy, requires window resize or focus change to fully apply
				// Win11 might not have this issue but Win10 does so please leave this
			}
		}

		public void WatchTheme(App app)
		{
			var currentUser = WindowsIdentity.GetCurrent();
			string query = string.Format(
				CultureInfo.InvariantCulture,
				@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{0}\\{1}' AND ValueName = '{2}'",
				currentUser.User.Value,
				RegistryKeyPath.Replace(@"\", @"\\"),
				RegistryValueName);

			try
			{
				var watcher = new ManagementEventWatcher(query);
				watcher.EventArrived += (sender, args) =>
				{
					if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
					{
						string newWindowsTheme = GetWindowsTheme();
						ChangeThemePath(newWindowsTheme.ToString(), app);
					}
				};

				watcher.Start();
			}
			catch { MessageBox.Show("Unable to fetch Windows theme.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }

			ChangeAppTheme(app);
		}

		public void ChangeAppTheme(App app)
		{
			if (Settings.Default.GuiTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
			{
				ChangeThemePath(GetWindowsTheme(), app);
			}
			else
			{
				ChangeThemePath(Settings.Default.GuiTheme, app);
			}
		}

		private void ChangeThemePath(string newTheme, App app)
		{
			string[] themeFiles = Directory.GetFiles("Themes", "*.xaml");

			string newThemeString = $"{Path.Combine("Themes", newTheme.ToString())}.xaml";
			foreach (string themeFile in themeFiles)
			{
				if (newThemeString.Equals(themeFile, StringComparison.OrdinalIgnoreCase))
				{
					var dictionary = new ResourceDictionary();

					XmlSerializer xmlReader = new XmlSerializer(typeof(ResourceDictionaryModel));
					using StreamReader streamReader = new StreamReader(themeFile);
					ResourceDictionaryModel themeValues = (ResourceDictionaryModel)xmlReader.Deserialize(streamReader);

					foreach (SolidColorBrushModel solidBrush in themeValues.SolidColorBrush)
					{
						app.Resources[solidBrush.Key] = (SolidColorBrush)new BrushConverter().ConvertFrom(solidBrush.Color);
					}
					return;
				}
			}
		}

		private void WriteDefaultThemes()
		{
			var resourceNames = GetResourceNames();
			var themePaths = resourceNames.Where((i) => i.StartsWith($"{nameof(TwitchDownloader)}.Themes."));

			foreach (var themePath in themePaths)
			{
				var themeData = ReadResource(themePath);
				var themePathSplit = themePath.Split(".");
				var themeName = themePathSplit[^2];
				var themeExtension = themePathSplit[^1];
				File.WriteAllText(Path.Combine("Themes", $"{themeName}.{themeExtension}"), themeData);
			}
		}

		private string[] GetResourceNames()
		{
			var asm = Assembly.GetExecutingAssembly();
			return asm.GetManifestResourceNames();
		}

		private string ReadResource(string resourcePath)
		{
			var asm = Assembly.GetExecutingAssembly();

			using (var stream = asm.GetManifestResourceStream(resourcePath))
			using (var reader = new StreamReader(stream))
			{
				return reader.ReadToEnd();
			}
		}

		private string GetWindowsTheme()
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
			{
				object registryValueObject = key?.GetValue(RegistryValueName);
				if (registryValueObject == null)
				{
					return "Light";
				}

				int registryValue = (int)registryValueObject;

				WindowsIsDarkTheme = registryValue > 0 ? false : true;

				return registryValue > 0 ? "Light" : "Dark";
			}
		}
		//https://engy.us/blog/2018/10/20/dark-theme-in-wpf/
	}
}
