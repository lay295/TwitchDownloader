using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderAvalonia.Views;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderAvalonia
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
#if DEBUG
            this.AttachDeveloperTools();
#endif
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            CoreLicensor.EnsureFilesExist(null);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var settings = new SettingsService();
                ThemeService.Apply(settings.Current.GuiTheme);
                var status = new AppStatus(settings);
                var ffmpeg = new FfmpegService();
                var files = new FileDialogService();
                var dialogs = new DialogService(settings, files);
                var collision = new FileCollisionService(settings, dialogs);
                var thumbnails = new ThumbnailService();
                var queue = new QueueService(settings, status, dialogs);
                var updates = new UpdateCheckService();

                var mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(settings, status, ffmpeg, dialogs, files, collision, thumbnails, queue, updates),
                };

                dialogs.SetOwner(mainWindow);
                files.SetOwner(mainWindow);
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}