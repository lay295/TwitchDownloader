using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.Tests.Fakes;
using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class SearchAndEnqueueSettingsTests
    {
        [Fact]
        public void SearchViewModelRestoresAndPersistsFilters()
        {
            var root = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var settings = new SettingsService(Path.Combine(root, "avalonia-settings.json"));
            settings.Current.Search.Kind = SearchKind.Clips;
            settings.Current.Search.VideoType = "ARCHIVE";
            settings.Current.Search.ClipPeriod = "LAST_DAY";
            settings.Current.Search.PageSize = 100;
            settings.Save();

            using var vm = CreateSearch(settings);
            Assert.Equal(SearchKind.Clips, vm.Kind);
            Assert.Equal("ARCHIVE", vm.SelectedVideoType?.Value);
            Assert.Equal("LAST_DAY", vm.SelectedClipPeriod?.Value);
            Assert.Equal(100, vm.SelectedPageSize);

            vm.Kind = SearchKind.Videos;
            vm.SelectedVideoType = vm.VideoTypes.First(option => option.Value == "HIGHLIGHT");
            vm.SelectedPageSize = 16;

            Assert.Equal(SearchKind.Videos, settings.Current.Search.Kind);
            Assert.Equal("HIGHLIGHT", settings.Current.Search.VideoType);
            Assert.Equal(16, settings.Current.Search.PageSize);
        }

        [Fact]
        public void EnqueueOptionsRememberCheckboxes()
        {
            var root = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var settings = new SettingsService(Path.Combine(root, "avalonia-settings.json"));
            EnqueueOptions? closed = null;

            var first = new EnqueueOptionsViewModel(
                TestLocalization.Instance,
                settings,
                new FakeFileDialogService(),
                hasVods: true,
                hasRecordingVods: true,
                close => closed = close)
            {
                Folder = root,
                DownloadVideo = true,
                DownloadChat = true,
                RenderChat = true,
                DelayVideo = true,
                DelayChat = true,
            };

            first.AddCommand.Execute(null);
            Assert.NotNull(closed);
            Assert.True(settings.Current.Queue.EnqueueDownloadChat);
            Assert.True(settings.Current.Queue.EnqueueRenderChat);
            Assert.True(settings.Current.Queue.EnqueueDelayVideo);
            Assert.True(settings.Current.Queue.EnqueueDelayChat);

            var second = new EnqueueOptionsViewModel(
                TestLocalization.Instance,
                settings,
                new FakeFileDialogService(),
                hasVods: true,
                hasRecordingVods: true,
                _ => { });

            Assert.True(second.DownloadChat);
            Assert.True(second.RenderChat);
            Assert.True(second.DelayVideo);
            Assert.True(second.DelayChat);
        }

        private static SearchViewModel CreateSearch(SettingsService settings)
        {
            var loc = TestLocalization.Instance;
            var status = new AppStatus(loc, settings);
            var dialogs = new FakeDialogService();
            var queue = new QueueService(loc, settings, status, dialogs);
            var ffmpeg = new FfmpegService(loc);
            var collision = new FileCollisionService(settings, dialogs);
            var cache = new AbandonedVideoCacheService(dialogs);
            var thumbnails = new ThumbnailService(new FakeHttpClientFactory(new HttpClientHandler()));
            var enqueue = new QueueEnqueueService(loc, settings, dialogs, queue, ffmpeg, collision, cache);
            return new SearchViewModel(loc, settings, status, dialogs, thumbnails, _ => Task.CompletedTask, enqueue);
        }
    }
}
