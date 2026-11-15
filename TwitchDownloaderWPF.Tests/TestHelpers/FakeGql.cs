using System.Net;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.TestHelpers
{
    /// <summary>
    /// Routes <c>TwitchHelper</c>'s static HttpClient to a fake handler for the duration of a test.
    /// Tests using this must be in <see cref="StaticHttpClientCollection"/>.
    /// </summary>
    internal static class FakeGql
    {
        public static HttpClientOverride Use(FakeHttpMessageHandler handler) =>
            new(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, handler);

        /// <summary>A handler answering every GetVideoInfo query with the given video status (e.g. "RECORDING"), or HTTP 500 when null.</summary>
        public static FakeHttpMessageHandler VideoStatus(string? status) => new((_, _) => status is null
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : FakeHttpMessageHandler.Json("""{"data":{"video":{"status":"STATUS","thumbnailURLs":[]}}}""".Replace("STATUS", status)));
    }
}
