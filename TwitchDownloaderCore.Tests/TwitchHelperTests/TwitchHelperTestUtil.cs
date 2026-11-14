using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using SkiaSharp;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Shared plumbing for the TwitchHelper tests: swapping TwitchHelper.httpClient, URL-routed fake responses,
    // simulated network failures, tiny real images and chat comment builders.
    internal static class TwitchHelperTestUtil
    {
        public const string GQL_URL = "https://gql.twitch.tv/gql";
        public const string WEB_CLIENT_ID = "kimne78kx3ncx6brgo4mv6wki5h1ko";

        /// <summary>Points <see cref="TwitchHelper.httpClient"/> at <paramref name="handler"/> until disposed.</summary>
        public static HttpClientOverride UseHandler(HttpMessageHandler handler, TimeSpan? timeout = null)
        {
            var httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, handler);
            if (timeout is { } t) TwitchHelper.httpClient.Timeout = t;
            return httpOverride;
        }

        /// <summary>Answers each request by its exact absolute URL; anything unmapped gets 404 Not Found.</summary>
        public static FakeHttpMessageHandler Routes(params (string Url, Func<HttpResponseMessage> Respond)[] routes)
        {
            var map = routes.ToDictionary(r => r.Url, r => r.Respond);
            return new FakeHttpMessageHandler((req, _) => map.TryGetValue(req.RequestUri!.AbsoluteUri, out var respond) ? respond() : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public static Func<HttpResponseMessage> Json(string json) => () => FakeHttpMessageHandler.Json(json);
        public static Func<HttpResponseMessage> Bytes(byte[] bytes) => () => FakeHttpMessageHandler.Bytes(bytes);
        public static Func<HttpResponseMessage> Status(HttpStatusCode code) => () => new HttpResponseMessage(code);

        public static string[] RequestedUrls(FakeHttpMessageHandler handler) => handler.Requests.Select(r => r.Uri.AbsoluteUri).ToArray();

        // Network failure kinds the provider-error logging distinguishes between, and the message each must produce.
        public const string FAIL_STATUS = "status", FAIL_SSL = "ssl", FAIL_TIMEOUT = "timeout", FAIL_OTHER = "other";

        public static string ExpectedFailureMessage(string kind, string providerName) => kind switch
        {
            FAIL_STATUS => $"{providerName} returned 500: InternalServerError.",
            FAIL_SSL => "The SSL connection could not be established, see inner exception: The remote certificate is invalid.",
            FAIL_TIMEOUT => $"{providerName} timed out.",
            _ => "Simulated failure."
        };

        /// <summary>
        /// A handler that serves <paramref name="routes"/> but fails every request matching <paramref name="shouldFail"/> in the way named by <paramref name="kind"/>.
        /// Timeouts are real: the request hangs until the (short) HttpClient.Timeout fires, so the genuine timeout exception is produced.
        /// </summary>
        public static HttpMessageHandler Failing(string kind, Func<Uri, bool> shouldFail, params (string Url, Func<HttpResponseMessage> Respond)[] routes)
        {
            var ok = Routes(routes);
            var invoker = new HttpMessageInvoker(ok);
            return new AsyncHandler(async (req, ct) =>
            {
                if (!shouldFail(req.RequestUri!))
                    return await invoker.SendAsync(req, ct);

                switch (kind)
                {
                    case FAIL_STATUS:
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    case FAIL_SSL:
                        throw new HttpRequestException("The SSL connection could not be established, see inner exception.", new AuthenticationException("The remote certificate is invalid."));
                    case FAIL_TIMEOUT:
                        await Task.Delay(Timeout.Infinite, ct);
                        throw new UnreachableException();
                    default:
                        throw new InvalidOperationException("Simulated failure.");
                }
            });
        }

        private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
        }

        // Third-party emote API endpoints for streamer 123.
        public const int STREAMER = 123;
        public const string BTTV_GLOBAL = "https://api.betterttv.net/3/cached/emotes/global";
        public const string BTTV_CHANNEL = "https://api.betterttv.net/3/cached/users/twitch/123";
        public const string FFZ_GLOBAL = "https://api.betterttv.net/3/cached/frankerfacez/emotes/global";
        public const string FFZ_CHANNEL = "https://api.betterttv.net/3/cached/frankerfacez/users/twitch/123";
        public const string STV_GLOBAL = "https://7tv.io/v3/emote-sets/global";
        public const string STV_USER = "https://7tv.io/v3/users/twitch/123";
        public const string STV_SET = "https://7tv.io/v3/emote-sets/01SET";

        // Builders for BTTV / FFZ / 7TV API payloads, shaped like the real responses.
        public static string Ser(object o) => JsonSerializer.Serialize(o);

        public static object Bttv(string id, string code, string imageType = "png") => new { id, code, imageType, animated = imageType == "gif", userId = "5561169bd6b9d206222a8c19" };

        public static object Ffz(int id, string code, bool animated = false) => new
        {
            id, code, animated, imageType = animated ? "webp" : "png", modifier = false,
            user = new { id = 1, name = "owner", displayName = "Owner" },
            images = new Dictionary<string, string> { ["1x"] = $"https://cdn.frankerfacez.com/emote/{id}/1" }
        };

        public static object Stv(string id, string name, StvEmoteFlags flags = 0, bool listed = true, params string[] formats) => new
        {
            id, name, flags = 0, timestamp = 1700000000000, actor_id = (string?)null,
            data = new
            {
                id, name, flags = (int)flags, lifecycle = 3, listed, animated = false,
                host = new { url = $"//cdn.7tv.app/emote/{id}", files = (formats.Length == 0 ? ["AVIF", "WEBP"] : formats).Where(f => f != "").Select(f => new { name = $"2x.{f.ToLowerInvariant()}", format = f, width = 64, height = 64, frame_count = 1, size = 100 }) }
            }
        };

        public static string StvSet(params object[] emotes) => Ser(new { id = "01GLOBAL", name = "Global Emotes", emotes });

        public const string STV_USER_JSON = """{"id":"01USER","platform":"TWITCH","username":"streamer","display_name":"Streamer","emote_capacity":1000,"emote_set_id":"01SET"}""";

        // Writes a metadata cache file exactly the way a previous successful session would have.
        public static void WriteCache(string path, params EmoteResponseItem[] items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var gz = new GZipStream(File.Create(path), CompressionLevel.SmallestSize);
            JsonSerializer.Serialize(gz, items.ToList());
        }

        public static EmoteResponseItem Item(string id, string code) => new() { Id = id, Code = code, ImageType = "png", ImageUrl = $"https://cdn.example/{id}/[scale]x" };

        public static byte[] Png(int width = 4, int height = 4) => Encode(SKEncodedImageFormat.Png, width, height);

        public static byte[] Webp(int width = 4, int height = 4) => Encode(SKEncodedImageFormat.Webp, width, height);

        // Skia cannot encode GIF, so use the classic 43-byte 1x1 GIF89a.
        public static byte[] Gif() => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        public static readonly byte[] NotAnImage = "this is not an image"u8.ToArray();

        private static byte[] Encode(SKEncodedImageFormat format, int width, int height)
        {
            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(SKColors.Red);
            using var data = bitmap.Encode(format, 100);
            return data.ToArray();
        }

        public static Comment Comment(string body, int bitsSpent = 0, string? logo = null, string[]? emoteIds = null, string[]? badgeIds = null) => new()
        {
            _id = "comment-id",
            commenter = new Commenter { _id = "1000", name = "viewer", display_name = "Viewer", logo = logo },
            message = new Message
            {
                body = body,
                bits_spent = bitsSpent,
                fragments = emoteIds?.Select(id => new Fragment { text = id, emoticon = new Emoticon { emoticon_id = id } }).ToList(),
                user_badges = badgeIds?.Select(id => new UserBadge { _id = id, version = "1" }).ToList(),
            }
        };
    }
}
