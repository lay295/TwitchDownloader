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
                var status = new AppStatus(settings);
                var ffmpeg = new FfmpegService();
                var dialogs = new DialogService();
                var files = new FileDialogService();
                var collision = new FileCollisionService(settings, dialogs);
                var thumbnails = new ThumbnailService();

                var mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(settings, status, ffmpeg, dialogs, files, collision, thumbnails),
                };

                dialogs.SetOwner(mainWindow);
                files.SetOwner(mainWindow);
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}