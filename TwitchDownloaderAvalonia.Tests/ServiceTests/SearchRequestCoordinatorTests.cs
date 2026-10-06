using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class SearchRequestCoordinatorTests
    {
        [Fact]
        public void CancelsPreviousRequestWhenStartingANewOne()
        {
            using var coordinator = new SearchRequestCoordinator();

            var first = coordinator.StartNew();
            var second = coordinator.StartNew();

            Assert.True(first.IsCancellationRequested);
            Assert.False(second.IsCancellationRequested);
            Assert.False(coordinator.IsCurrent(first));
            Assert.True(coordinator.IsCurrent(second));
        }
    }
}
