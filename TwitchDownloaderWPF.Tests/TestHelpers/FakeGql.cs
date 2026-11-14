using System.Net;
using System.Reflection;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.TestHelpers
{
    /// <summary>
    /// Routes <c>TwitchHelper</c>'s static HttpClient to a fake handler for the duration of a test. Core only exposes that field to its own
    /// test projects, so it is swapped via reflection. Tests using this must be in <see cref="StaticHttpClientCollection"/>.
    /// </summary>
    internal static class FakeGql
    {
        private static readonly FieldInfo HttpClientField = typeof(TwitchHelper).GetField("httpClient", BindingFlags.NonPublic | BindingFlags.Static)!;

        public static HttpClientOverride Use(FakeHttpMessageHandler handler) =>
            new(c => HttpClientField.SetValue(null, c), () => (HttpClient)HttpClientField.GetValue(null)!, handler);

        /// <summary>A handler answering every GetVideoInfo query with the given video status (e.g. "RECORDING"), or HTTP 500 when null.</summary>
        public static FakeHttpMessageHandler VideoStatus(string? status) => new((_, _) => status is null
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : FakeHttpMessageHandler.Json("""{"data":{"video":{"status":"STATUS","thumbnailURLs":[]}}}""".Replace("STATUS", status)));
    }
}
