using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class FileCollisionServiceTests
    {
        [Fact]
        public async Task RemembersChoiceForParallelCallers()
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"), "settings.json");
            var settings = new SettingsService(settingsPath);
            settings.Current.General.FileCollisionBehavior = CollisionBehavior.Prompt;

            var file = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4"));
            var expectedName = file.Name;
            var expectedPath = file.FullName;
            var prompts = 0;
            var service = new FileCollisionService(settings, PromptOverwrite);
            var cancellation = TestContext.Current.CancellationToken;

            var tasks = new Task[8];
            for (var i = 0; i < tasks.Length; i++)
                tasks[i] = Task.Run(() => service.HandleCollision(file), cancellation);

            await Task.WhenAll(tasks).WaitAsync(cancellation);

            Assert.Equal(1, prompts);
            return;

            CollisionPromptResult PromptOverwrite(string fileName, string fullPath)
            {
                Assert.Equal(expectedName, fileName);
                Assert.Equal(expectedPath, fullPath);
                Interlocked.Increment(ref prompts);
                Thread.Sleep(50);
                return new CollisionPromptResult(CollisionChoice.Overwrite, Remember: true);
            }
        }
    }
}
