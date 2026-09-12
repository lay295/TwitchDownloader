using System.Net;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class UpdateCheckServiceTests
    {
        [Fact]
        public async Task FailedCheckDoesNotCacheFailure()
        {
            var handler = new SequenceHandler();
            handler.EnqueueError(new HttpRequestException("offline"));
            handler.EnqueueXml("<item><version>9.9.9</version><changelog>https://example.com</changelog></item>");

            var service = new UpdateCheckService(new HttpClient(handler));
            var local = new Version(1, 0, 0);

            var first = await service.CheckAsync(local, TestContext.Current.CancellationToken);
            var second = await service.CheckAsync(local, TestContext.Current.CancellationToken);

            Assert.Null(first);
            Assert.NotNull(second);
            Assert.Equal(new Version(9, 9, 9), second.RemoteVersion);
            Assert.Equal(2, handler.Calls);
        }

        private sealed class SequenceHandler : HttpMessageHandler
        {
            private readonly Queue<Func<HttpResponseMessage>> _responses = new();

            public int Calls { get; private set; }

            public void EnqueueError(Exception exception) => _responses.Enqueue(() => throw exception);

            public void EnqueueXml(string xml)
            {
                _responses.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(xml),
                });
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(_responses.Dequeue()());
            }
        }
    }
}
