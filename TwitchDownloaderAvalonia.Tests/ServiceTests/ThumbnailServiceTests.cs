using System.Net;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class ThumbnailServiceTests
    {
        [Fact]
        public async Task RejectsPayloadLargerThanCap()
        {
            var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[ThumbnailService.MAX_BYTES + 1]),
            });
            var service = new ThumbnailService(new HttpClient(handler));

            var bytes = await service.TryGetAsync("https://example.com/thumb.jpg", TestContext.Current.CancellationToken);

            Assert.Null(bytes);
        }

        private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(response);
            }
        }
    }
}
