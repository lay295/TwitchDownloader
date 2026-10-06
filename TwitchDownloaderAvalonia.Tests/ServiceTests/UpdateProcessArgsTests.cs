using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class UpdateProcessArgsTests
    {
        [Fact]
        public void RoundTripsArguments()
        {
            var original = new UpdateProcessArgs(4242, "/opt/TwitchDownloader", "1.56.5", DryRun: true, Culture: "ru-RU");
            var parsed = UpdateProcessArgs.Parse(original.ToArguments());

            Assert.Equal(original.ParentPid, parsed.ParentPid);
            Assert.Equal(original.InstallDirectory, parsed.InstallDirectory);
            Assert.Equal(original.LocalVersion, parsed.LocalVersion);
            Assert.Equal(original.DryRun, parsed.DryRun);
            Assert.Equal(original.Culture, parsed.Culture);
        }

        [Fact]
        public void ParsesCultureAndDryRun()
        {
            var parsed = UpdateProcessArgs.Parse(["--pid", "12", "--debug-dry-run", "--local-version", "1.0.0", "--culture", "ru-RU"]);
            Assert.True(parsed.DryRun);
            Assert.Equal(12, parsed.ParentPid);
            Assert.Equal("1.0.0", parsed.LocalVersion);
            Assert.Equal("ru-RU", parsed.Culture);
        }

        [Fact]
        public async Task ShutdownSignalCreatesMarkerFile()
        {
            var pid = Random.Shared.Next(10_000, 99_999);
            UpdateShutdownSignal.Clear(pid);
            try
            {
                Assert.True(UpdateShutdownSignal.Signal(pid));
                await UpdateShutdownSignal.WaitAsync(pid, TestContext.Current.CancellationToken);
                Assert.True(File.Exists(UpdateShutdownSignal.GetPath(pid)));
            }
            finally
            {
                UpdateShutdownSignal.Clear(pid);
            }
        }
    }
}
