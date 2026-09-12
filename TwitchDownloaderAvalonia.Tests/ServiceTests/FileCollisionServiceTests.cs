using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class FileCollisionServiceTests
    {
        [Fact]
        public void RemembersChoiceForParallelCallers()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"), "settings.json");
            var settings = new SettingsService(settingsPath) { Current = { FileCollisionBehavior = CollisionBehavior.Prompt } };

            var prompts = 0;
            var service = new FileCollisionService(settings, (_, _) =>
            {
                Interlocked.Increment(ref prompts);
                Thread.Sleep(50);
                return new CollisionPromptResult(CollisionChoice.Overwrite, Remember: true);
            });

            var file = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4"));
            Parallel.For(0, 8, _ => service.HandleCollision(file));

            Assert.Equal(1, prompts);
        }
    }
}
