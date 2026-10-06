using System.Net;
using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class GitHubReleaseNotesClientTests
    {
        [Fact]
        public async Task ReturnsBodyAndPublishedAtWhenPresent()
        {
            var handler = new JsonHandler(HttpStatusCode.OK, """{"body":"# Notes\n\n- Fix","published_at":"2026-09-07T18:30:00Z"}""");
            var client = new GitHubReleaseNotesClient(new HttpClient(handler, disposeHandler: false));

            var notes = await client.GetReleaseAsync("1.56.5", TestContext.Current.CancellationToken);

            Assert.NotNull(notes);
            Assert.Equal("# Notes\n\n- Fix", notes.Body);
            Assert.Equal(new DateTimeOffset(2026, 9, 7, 18, 30, 0, TimeSpan.Zero), notes.PublishedAt);
            Assert.Contains("/releases/tags/1.56.5", handler.LastUri?.ToString());
        }

        [Fact]
        public async Task ReturnsNullOnNotFound()
        {
            var handler = new JsonHandler(HttpStatusCode.NotFound, """{"message":"Not Found"}""");
            var client = new GitHubReleaseNotesClient(new HttpClient(handler, disposeHandler: false));

            Assert.Null(await client.GetReleaseAsync("9.9.9", TestContext.Current.CancellationToken));
        }

        private sealed class JsonHandler(HttpStatusCode status, string json) : HttpMessageHandler
        {
            public Uri? LastUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(json),
                });
            }
        }
    }
}
