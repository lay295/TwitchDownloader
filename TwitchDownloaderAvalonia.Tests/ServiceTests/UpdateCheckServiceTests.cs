using System.Net;
using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Services;

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

            var service = new UpdateCheckService(new HttpClient(handler, disposeHandler: false));
            var local = new Version(1, 0, 0);

            var first = await service.CheckAsync(local, TestContext.Current.CancellationToken);
            var second = await service.CheckAsync(local, TestContext.Current.CancellationToken);

            Assert.Null(first);
            Assert.NotNull(second);
            Assert.Equal(new Version(9, 9, 9), second.RemoteVersion);
            Assert.Null(second.AvaloniaUrlTemplate);
            Assert.Equal(2, handler.Calls);
        }

        [Fact]
        public async Task ParsesUrlAvaloniaAndChangelog()
        {
            var handler = new SequenceHandler();
            handler.EnqueueXml("""
                <item>
                  <version>2.0.0</version>
                  <url>https://example.com/gui.zip</url>
                  <url-cli>https://example.com/cli-{0}.zip</url-cli>
                  <url-avalonia>https://example.com/TwitchDownloaderAvalonia-2.0.0-{0}.zip</url-avalonia>
                  <changelog>https://example.com/notes</changelog>
                </item>
                """);

            var service = new UpdateCheckService(new HttpClient(handler, disposeHandler: false));
            var result = await service.CheckAsync(new Version(1, 0, 0), TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.True(result.IsNewer);
            Assert.Equal("https://example.com/notes", result.ChangelogUrl);
            Assert.Equal("https://example.com/TwitchDownloaderAvalonia-2.0.0-{0}.zip", result.AvaloniaUrlTemplate);
        }

        [Fact]
        public async Task MissingChangelogFallsBackAndIgnoresWpfUrl()
        {
            var handler = new SequenceHandler();
            handler.EnqueueXml("<item><version>1.0.0</version><url>https://example.com/wpf.zip</url></item>");

            var service = new UpdateCheckService(new HttpClient(handler, disposeHandler: false));
            var result = await service.CheckAsync(new Version(1, 0, 0), TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.False(result.IsNewer);
            Assert.Equal(UpdateCheckService.DEFAULT_CHANGELOG_URL, result.ChangelogUrl);
            Assert.Null(result.AvaloniaUrlTemplate);
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
