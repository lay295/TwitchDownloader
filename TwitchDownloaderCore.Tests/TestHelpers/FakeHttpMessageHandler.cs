using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace TwitchDownloaderCore.Tests.TestHelpers
{
    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that never touches the network. Every request is answered by the
    /// <c>respond</c> callback supplied by the test, and is recorded (with its body) so tests can assert on
    /// exactly what the production code sent to Twitch/BTTV/FFZ/7TV/etc.
    /// </summary>
    /// <remarks>
    /// Thread safe: <see cref="TwitchDownloaderCore.Tools.VideoDownloadThread"/> issues requests from many threads at once.
    /// </remarks>
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body, IReadOnlyDictionary<string, string> Headers);

        private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _respond;

        public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

        /// <param name="respond">Receives the request and its body (already read as a string, or null if there was none).</param>
        public FakeHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        /// <summary>A handler that answers every request with <c>404 Not Found</c>, for tests that must not hit the network.</summary>
        public static FakeHttpMessageHandler NotFound() => new((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        public static HttpResponseMessage Text(string text, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(text, Encoding.UTF8, "text/plain") };

        public static HttpResponseMessage Bytes(byte[] bytes, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new ByteArrayContent(bytes) };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            Requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, body, headers));

            var response = _respond(request, body);
            response.RequestMessage ??= request;
            return response;
        }
    }
}
