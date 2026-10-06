using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class AbandonedVideoCacheServiceTests
    {
        [Fact]
        public void PromptAbandonedVideoCachesReturnsEmptyWhenOwnerIsMissing()
        {
            var dialogs = CreateDialogs();
            var dir = new DirectoryInfo(Path.GetTempPath());

            Assert.Empty(dialogs.PromptAbandonedVideoCaches([dir]));
            Assert.Empty(dialogs.PromptAbandonedVideoCaches([]));
        }

        [Fact]
        public async Task EnsureBackgroundThreadThrowsOnUiThread()
        {
            void AssertThrows() =>
                Assert.Throws<InvalidOperationException>(() =>
                    DialogService.EnsureBackgroundThread("Abandoned video cache prompts"));

            if (Dispatcher.UIThread.CheckAccess())
            {
                AssertThrows();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(AssertThrows);
        }

        [Fact]
        public void HandleSerializesConcurrentPrompts()
        {
            var prompts = 0;
            var service = new AbandonedVideoCacheService(_ =>
            {
                Interlocked.Increment(ref prompts);
                Thread.Sleep(50);
                return [];
            });

            var dir = new DirectoryInfo(Path.GetTempPath());
            Parallel.For(0, 8, _ => service.Handle([dir]));

            Assert.Equal(8, prompts);
        }

        [Fact]
        public void HandleReturnsEmptyForNoDirectories()
        {
            var prompts = 0;
            var service = new AbandonedVideoCacheService(_ =>
            {
                Interlocked.Increment(ref prompts);
                return [];
            });

            Assert.Empty(service.Handle([]));
            Assert.Equal(0, prompts);
        }

        private static DialogService CreateDialogs()
        {
            var loc = TestLocalization.Instance;
            var settingsPath = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"), "settings.json");
            return new DialogService(loc, new SettingsService(settingsPath), new FileDialogService(loc), NullLoggerFactory.Instance);
        }
    }
}
