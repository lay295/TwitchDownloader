using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TwitchDownloaderAvalonia.DependencyInjection;

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
                DesktopBootstrapper.Run(desktop);

            base.OnFrameworkInitializationCompleted();
        }
    }
}
