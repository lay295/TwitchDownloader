using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Tests.Fakes;
using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class VodDownloadViewModelTests
    {
        [Fact]
        public async Task InvalidUrlShowsErrorWithoutWindow()
        {
            var loc = TestLocalization.Instance;

            var root = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var settings = new SettingsService(Path.Combine(root, "avalonia-settings.json"));
            var status = new AppStatus(loc, settings);
            var dialogs = new FakeDialogService();
            var files = new FakeFileDialogService();
            var thumbnails = new ThumbnailService(new FakeHttpClientFactory(new HttpClientHandler()));
            using var vm = new VodDownloadViewModel(
                loc,
                settings,
                status,
                new FfmpegService(loc),
                dialogs,
                files,
                new FileCollisionService(settings, dialogs),
                new AbandonedVideoCacheService(dialogs),
                thumbnails,
                new QueueService(loc, settings, status, dialogs));

            vm.VideoUrl = "not-a-vod";
            await vm.GetInfoCommand.ExecuteAsync(null);

            Assert.Single(dialogs.Errors);
            Assert.Equal(loc.Get("vod.invalid_title"), dialogs.Errors[0].Title);
            Assert.Equal(loc.Get("vod.invalid_message"), dialogs.Errors[0].Message);
            Assert.False(vm.IsBusy);
            Assert.True(vm.ShowIdlePreview);
            Assert.Equal(loc.Get("common.get_info"), vm.GetInfoButtonText);
        }
    }
}
