using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchDownloaderAvalonia.DependencyInjection;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Services;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderAvalonia.Views;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void RegistersNamedHttpClientsAndMainWindowViewModel()
        {
            var root = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var settings = new SettingsService(Path.Combine(root, "avalonia-settings.json"));

            using var provider = new ServiceCollection()
                .AddTwitchDownloader(Path.Combine(root, "logs"))
                .AddSingleton(settings)
                .BuildServiceProvider();

            var factory = provider.GetRequiredService<IHttpClientFactory>();
            using var thumbnails = factory.CreateClient(HttpClientNames.Thumbnails);
            using var updates = factory.CreateClient(HttpClientNames.Updates);

            Assert.Equal(TimeSpan.FromSeconds(15), thumbnails.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(15), updates.Timeout);
            Assert.True(updates.DefaultRequestHeaders.TryGetValues("User-Agent", out var userAgent));
            Assert.Contains("TwitchDownloader", userAgent);

            Assert.NotNull(provider.GetRequiredService<ThumbnailService>());
            Assert.NotNull(provider.GetRequiredService<UpdateCheckService>());
            Assert.NotNull(provider.GetRequiredService<MainWindowViewModel>());
            Assert.Null(provider.GetService<MainWindow>());

            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("test");
            logger.LogInformation("Service graph resolved");
            var logFile = Path.Combine(root, "logs", "avalonia.log");
            Assert.True(File.Exists(logFile));
            Assert.Contains("Service graph resolved", File.ReadAllText(logFile));
        }
    }
}
