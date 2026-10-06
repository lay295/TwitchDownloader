using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderAvalonia.Views;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal static class DesktopBootstrapper
    {
        public static void Run(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection()
                .AddTwitchDownloader()
                .BuildServiceProvider();

            desktop.Exit += OnExit;

            var loggers = services.GetRequiredService<ILoggerFactory>();
            var settings = services.GetRequiredService<SettingsService>();
            var localization = services.GetRequiredService<LocalizationService>();
            var themes = services.GetRequiredService<ThemeService>();
            localization.SetCulture(settings.Current.Ui.Culture);
            var themeStartup = themes.Initialize(settings);

            var vm = services.GetRequiredService<MainWindowViewModel>();
            var dialogs = services.GetRequiredService<DialogService>();
            var files = services.GetRequiredService<FileDialogService>();
            var logger = loggers.CreateLogger(nameof(DesktopBootstrapper));
            var logFile = services.GetRequiredService<RollingFileLoggerProvider>().FilePath;
            logger.LogInformation("Started. Logs path: {Path}", logFile);

            var mainWindow = new MainWindow { DataContext = vm };
            mainWindow.Opened += OnOpened;
            dialogs.SetOwner(mainWindow);
            files.SetOwner(mainWindow);
            desktop.MainWindow = mainWindow;
            return;

            void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => services.Dispose();

            void OnOpened(object? sender, EventArgs e)
            {
                mainWindow.Opened -= OnOpened;
                Observe(InitializeMainWindowAsync(vm, dialogs, localization, themeStartup, logger), logger);
            }
        }

        private static void Observe(Task task, ILogger logger)
        {
            task.ContinueWith(
                completed =>
                {
                    if (completed.Exception is { } ex)
                        logger.LogError(ex.Flatten(), "Startup initialize failed");
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private static async Task InitializeMainWindowAsync(
            MainWindowViewModel vm,
            IDialogService dialogs,
            LocalizationService localization,
            ThemeStartupResult themeStartup,
            ILogger logger)
        {
            try
            {
                await ShowThemeStartupDialogsAsync(dialogs, localization, themeStartup);
                await vm.InitializeAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Startup initialize failed");
            }
        }

        private static async Task ShowThemeStartupDialogsAsync(
            IDialogService dialogs,
            LocalizationService localization,
            ThemeStartupResult themeStartup)
        {
            if (themeStartup.MissingPackFileName is { } fileName)
            {
                await dialogs.ShowMessageAsync(
                    localization.Get("settings.theme_not_found"),
                    localization.Get("settings.theme_not_found_message", fileName));
            }

            if (themeStartup.ThemesWriteFailed)
            {
                var message = localization.Get("settings.themes_failed_to_write");
                await dialogs.ShowMessageAsync(message, message);
            }
        }
    }
}
