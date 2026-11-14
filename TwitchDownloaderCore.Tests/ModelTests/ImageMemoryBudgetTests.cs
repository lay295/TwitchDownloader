using TwitchDownloaderCore.Models.Render;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    public class ImageMemoryBudgetTests
    {
        // 1x1 and 2x2 RGBA PNGs, so a decoded frame is 4 and 16 bytes respectively
        private const string PNG_1X1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4z8DwHwAFAAH/iZk9HQAAAABJRU5ErkJggg==";
        private const string PNG_2X2 = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAFElEQVR4nGP8z8Dwn4GBgYGJAQoAHxcCAk+Uzr4AAAAASUVORK5CYII=";

        private static TwitchEmote Image(string name, string base64 = PNG_1X1)
        {
            var emote = new TwitchEmote(Convert.FromBase64String(base64), null, EmoteProvider.ThirdParty, 1, name, name);
            _ = emote.EmoteFrames; // decode, so releasing it is observable
            Assert.True(emote.FramesMaterialized);
            return emote;
        }

        [Fact]
        public void KeepsEverythingWhileUnderBudget()
        {
            var images = new[] { Image("a"), Image("b") }; // 8 bytes
            var budget = new ImageMemoryBudget(64);
            budget.MarkDrawn(images);

            Assert.False(budget.ReleaseUnused(images, []));
            Assert.All(images, x => Assert.True(x.FramesMaterialized));
        }

        [Fact]
        public void ReleasesLeastRecentlyDrawnFirst()
        {
            var oldest = Image("oldest");
            var middle = Image("middle");
            var newest = Image("newest");

            var budget = new ImageMemoryBudget(8); // room for two of the three
            budget.MarkDrawn([oldest]);
            budget.MarkDrawn([middle]);
            budget.MarkDrawn([newest]);

            Assert.True(budget.ReleaseUnused([oldest, middle, newest], []));

            Assert.False(oldest.FramesMaterialized);
            Assert.True(middle.FramesMaterialized);
            Assert.True(newest.FramesMaterialized);
        }

        [Fact]
        public void NeverReleasesWhatIsOnScreen()
        {
            var onScreen = Image("visible");
            var offScreen = Image("hidden");

            var budget = new ImageMemoryBudget(1); // far below either image
            budget.MarkDrawn([onScreen, offScreen]);

            budget.ReleaseUnused([onScreen, offScreen], [onScreen]);

            Assert.True(onScreen.FramesMaterialized);
            Assert.False(offScreen.FramesMaterialized);
        }

        [Fact]
        public void OnScreenImagesDoNotCountTowardsTheBudget()
        {
            // A large on screen GIF must not push out the off screen emotes that fit on their own, otherwise they are
            // released and decoded again on every frame.
            var bigOnScreen = Image("gif", PNG_2X2); // 16 bytes, on screen
            var emoteA = Image("emoteA");            // 4 bytes
            var emoteB = Image("emoteB");            // 4 bytes

            var budget = new ImageMemoryBudget(8); // the two off screen emotes fit exactly
            budget.MarkDrawn([bigOnScreen, emoteA, emoteB]);

            Assert.False(budget.ReleaseUnused([bigOnScreen, emoteA, emoteB], [bigOnScreen]));

            Assert.True(emoteA.FramesMaterialized);
            Assert.True(emoteB.FramesMaterialized);
        }

        [Fact]
        public void ReleasedImagesDecodeAgainOnDemand()
        {
            var image = Image("reused");
            var budget = new ImageMemoryBudget(1);
            budget.MarkDrawn([image]);

            budget.ReleaseUnused([image], []);
            Assert.False(image.FramesMaterialized);

            Assert.NotEmpty(image.EmoteFrames);
            Assert.True(image.FramesMaterialized);
        }
    }
}
