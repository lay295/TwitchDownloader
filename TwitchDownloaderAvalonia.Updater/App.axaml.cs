using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;
using TwitchDownloaderAvalonia.Updater.ViewModels;
using TwitchDownloaderAvalonia.Updater.Views;

namespace TwitchDownloaderAvalonia.Updater
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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var args = UpdateProcessArgs.Parse(desktop.Args ?? []);
                var settings = UpdaterSettings.Load();
                RequestedThemeVariant = settings.ThemeVariant;
                var loc = new LocalizationService();
                loc.SetCulture(args.Culture ?? settings.Culture);
                var checkClient = CreateClient(TimeSpan.FromSeconds(15));
                var downloadClient = CreateClient(Timeout.InfiniteTimeSpan);
                var vm = new UpdateWindowViewModel(
                    loc,
                    args,
                    new UpdateCheckService(checkClient),
                    new AppUpdateService(downloadClient),
                    new GitHubReleaseNotesClient(checkClient),
                    new UpdatePreferencesStore(),
                    settings.VerboseErrors);

                desktop.MainWindow = new UpdateWindow { DataContext = vm };
                desktop.ShutdownRequested += (_, _) =>
                {
                    checkClient.Dispose();
                    downloadClient.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static HttpClient CreateClient(TimeSpan timeout)
        {
            var client = new HttpClient { Timeout = timeout };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TwitchDownloader");
            return client;
        }
    }

    public readonly record struct UpdaterUiSettings(ThemeVariant ThemeVariant, string Culture, bool VerboseErrors);

    public static class UpdaterSettings
    {
        public static UpdaterUiSettings Load()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwitchDownloader", "avalonia-settings.json");
            var theme = ThemeVariant.Default;
            var culture = LocalizationService.DEFAULT_CULTURE;
            var verbose = false;

            try
            {
                if (!File.Exists(path))
                    return new UpdaterUiSettings(theme, culture, verbose);

                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                if (TryGetProperty(document.RootElement, "ui", out var ui))
                {
                    if (TryGetProperty(ui, "theme", out var themeElement))
                        theme = ParseTheme(themeElement.GetString());

                    if (TryGetProperty(ui, "culture", out var cultureElement))
                        culture = cultureElement.GetString() ?? culture;
                }

                if (TryGetProperty(document.RootElement, "general", out var general)
                    && TryGetProperty(general, "verboseErrors", out var verboseElement))
                    verbose = verboseElement.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            catch
            {
                // ignored
            }

            return new UpdaterUiSettings(theme, culture, verbose);
        }

        private static bool TryGetProperty(System.Text.Json.JsonElement element, string name, out System.Text.Json.JsonElement value)
        {
            if (element.TryGetProperty(name, out value))
                return true;

            foreach (var property in element.EnumerateObject())
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = property.Value;
                return true;
            }

            value = default;
            return false;
        }

        private static ThemeVariant ParseTheme(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "System", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Default;

            if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase) || value.Contains("dark", StringComparison.OrdinalIgnoreCase))
                return ThemeVariant.Dark;

            return ThemeVariant.Light;
        }
    }
}
