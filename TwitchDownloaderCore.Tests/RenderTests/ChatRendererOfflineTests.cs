using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    // Verifies ChatRenderer's offline promise ("render completely offline using only embedded emotes, badges, and bits"): with
    // Offline set, a render that touches every image source (badges, emotes, third-party providers, cheermotes, avatars,
    // the gift-many highlight icon) must not send a single HTTP request. Swaps the static TwitchHelper.httpClient, hence the collection.
    [Collection(StaticHttpClientCollection.NAME)]
    public class ChatRendererOfflineTests
    {
        /// <summary>
        /// Every HTTP request is recorded by a fake handler; an offline render must complete and record none. Guards against a new image
        /// source forgetting to honour the offline flag (which would hang or fail renders on machines without internet).
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_Offline_SendsNoHttpRequests()
        {
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, handler);
            using var harness = new RenderHarness();
            harness.Options.RenderUserAvatars = true;

            var video = await harness.RenderAsync(c => c.comments.Add(
                RenderHarness.MakeComment(2.5, "dave", "Dave is gifting 5 Tier 1 Subs to TestStreamer's community! ", displayName: "Dave",
                    logo: "https://static-cdn.jtvnw.net/jtv_user_pictures/dave-profile_image-70x70.png")), TestContext.Current.CancellationToken);

            Assert.Equal(30, video.FrameCount);
            Assert.Empty(handler.Requests);
        }
    }
}
