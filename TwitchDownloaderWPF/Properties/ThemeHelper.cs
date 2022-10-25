﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TwitchDownloaderWPF;

namespace TwitchDownloader.Properties
{
	public partial class ThemeHelper
	{
		public readonly App app;
		private bool WindowsIsDarkOrHighContrast { get; set; }

		[DllImport("dwmapi.dll", PreserveSig = true)]
		public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref bool attrValue, int attrSize);

		private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
		private const string RegistryValueName = "AppsUseLightTheme";

		private enum WindowsTheme
		{
			Light,
			Dark
		}

		public enum AppTheme
		{
			Light,
			Dark,
			HighContrast // Not yet implemented
		}

		public ThemeHelper(App _app)
		{
			app = _app;
		}

		public void UpdateTitleBarTheme(Window wnd)
		{
			if ((AppTheme)Settings.Default.GuiTheme == AppTheme.Dark || (Settings.Default.GuiTheme == -1 && WindowsIsDarkOrHighContrast))
			{
				bool isDarkTheme = true;
				DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(wnd).Handle, 20, ref isDarkTheme, Marshal.SizeOf(isDarkTheme));

				Window _wnd = new Window();
				_wnd.SizeToContent = SizeToContent.WidthAndHeight;
				_wnd.Show();
				_wnd.Close();
				// Dark title bar is a bit buggy, requires window resize or focus change to fully apply
				// Win11 might not have this issue but Win10 does so please leave this
			}
		}

		public void WatchTheme()
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
					WindowsIsDarkOrHighContrast = false;
					if (Settings.Default.GuiTheme == -1)
					{
						WindowsTheme newWindowsTheme = GetWindowsTheme();
						if (newWindowsTheme.ToString().Equals("Dark"))
						{
							WindowsIsDarkOrHighContrast = true;
						}
						ChangeThemePath((AppTheme)newWindowsTheme);
					}
				};

				watcher.Start();
			}
			catch (Exception ex) { MessageBox.Show(ex.Message.ToString()); }

			WindowsIsDarkOrHighContrast = false;
			// -1 = System, 0 = Light, 1 = Dark
			if (Settings.Default.GuiTheme == -1)
			{
				WindowsTheme initialTheme = GetWindowsTheme();
				if (initialTheme.ToString().Equals("Dark"))
				{
					WindowsIsDarkOrHighContrast = true;
				}
				ChangeThemePath((AppTheme)initialTheme);
			}
			else
			{
				ChangeThemePath((AppTheme)Settings.Default.GuiTheme);
			}
		}

		public void ChangeAppTheme()
		{
			if (Settings.Default.GuiTheme != -1)
			{
				ChangeThemePath((AppTheme)Settings.Default.GuiTheme);
			}
			else
			{
				WindowsTheme windowsTheme = GetWindowsTheme();
				ChangeThemePath((AppTheme)windowsTheme);
			}
		}

		private void ChangeThemePath(AppTheme newTheme)
		{
			app.Resources.MergedDictionaries[2].Source = new Uri($"Themes/{newTheme}.xaml", UriKind.Relative);
		}

		private static WindowsTheme GetWindowsTheme()
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
			{
				object registryValueObject = key?.GetValue(RegistryValueName);
				if (registryValueObject == null)
				{
					return WindowsTheme.Light;
				}

				int registryValue = (int)registryValueObject;

				return registryValue > 0 ? WindowsTheme.Light : WindowsTheme.Dark;
			}
		}
		//https://engy.us/blog/2018/10/20/dark-theme-in-wpf/
	}
}
