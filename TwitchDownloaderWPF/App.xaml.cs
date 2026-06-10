using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
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

            PromptImportSettingsIfNeeded();

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

        private static void MarkSettingsImported()
        {
            Settings.Default.SettingsImported = true;
            Settings.Default.UpgradeRequired = false;
            Settings.Default.Save();
        }

        private static void PromptImportSettingsIfNeeded()
        {
            if (Settings.Default.SettingsImported)
                return;

            string currentConfigPath;
            try
            {
                var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                currentConfigPath = cfg.FilePath;
            }
            catch
            {
                MarkSettingsImported();
                return;
            }

            try
            {
                // ...\<company>\<identity TwitchDownloaderWPF_xxx>\<version>\user.config
                var currentIdentityDir = Path.GetDirectoryName(Path.GetDirectoryName(currentConfigPath));
                var companyDir = Path.GetDirectoryName(currentIdentityDir);
                if (string.IsNullOrEmpty(companyDir) || !Directory.Exists(companyDir))
                {
                    MarkSettingsImported();
                    return;
                }

                var candidates = new List<SettingsImportCandidate>();
                foreach (var dir in Directory.GetDirectories(companyDir, "TwitchDownloaderWPF*"))
                {
                    if (string.Equals(dir, currentIdentityDir, StringComparison.OrdinalIgnoreCase))
                        continue;

                    FileInfo newest = null;
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(dir, "user.config", SearchOption.AllDirectories))
                        {
                            var fi = new FileInfo(file);
                            if (newest is null || fi.LastWriteTime > newest.LastWriteTime)
                                newest = fi;
                        }
                    }
                    catch { /* unreadable identity dir, skip */ }

                    if (newest is null)
                        continue;

                    var versionFolder = Path.GetFileName(Path.GetDirectoryName(newest.FullName));
                    var idFolder = Path.GetFileName(dir);
                    candidates.Add(new SettingsImportCandidate
                    {
                        ConfigPath = newest.FullName,
                        LastModified = newest.LastWriteTime,
                        DisplayName = $"{idFolder}  -  v{versionFolder}  -  modified {newest.LastWriteTime:g}"
                    });
                }

                if (candidates.Count == 0)
                {
                    MarkSettingsImported();
                    return;
                }

                candidates = candidates.OrderByDescending(c => c.LastModified).ToList();

                var dialog = new WindowSettingsImport(candidates);
                dialog.ShowDialog();

                if (!string.IsNullOrEmpty(dialog.SelectedConfigPath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(currentConfigPath)!);
                        File.Copy(dialog.SelectedConfigPath, currentConfigPath, true);
                        Settings.Default.Reload();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to import settings: " + ex.Message, "Import Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch
            {
                /* never block startup on import failure */
            }

            MarkSettingsImported();
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
            var result = MessageBox.Show(message, Translations.Strings.FatalError, MessageBoxButton.YesNo, MessageBoxImage.Error);

            if (result is MessageBoxResult.No)
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
