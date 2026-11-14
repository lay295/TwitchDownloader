using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.TwitchObjects.Gql;
using ClipVideoQuality = TwitchDownloaderCore.Models.ClipVideoQuality;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // Tests clip quality selection: VideoQualities.FromClip (naming, sorting, resolution and source detection), ClipVideoQuality,
    // and ClipVideoQualities (keyword/regex lookup). ClipDownloader resolves the user's --quality argument through this.
    public class ClipVideoQualitiesTests
    {
        private static ShareClipRenderStatusVideoQuality Quality(string quality, decimal frameRate, int width = 0, int height = 0, int bitrate = 1000) =>
            new() { quality = quality, frameRate = frameRate, width = width, height = height, bitrate = bitrate, sourceURL = $"https://clips.example/{quality}_{frameRate}_{width}_{bitrate}.mp4" };

        private static ShareClipRenderStatusAsset Landscape(params ShareClipRenderStatusVideoQuality[] qualities) =>
            new() { aspectRatio = 16 / 9m, thumbnailURL = "https://clips.example/landscape/thumb.jpg", videoQualities = qualities };

        private static ShareClipRenderStatusAsset Portrait(params ShareClipRenderStatusVideoQuality[] qualities) =>
            new() { aspectRatio = 9 / 16m, thumbnailURL = "https://clips.example/portrait/thumb.jpg", portraitMetadata = new PortraitMetadata(), videoQualities = qualities };

        // A landscape asset (listed in a shuffled order) plus a portrait asset, like Twitch's ShareClipRenderStatus response.
        private static ShareClipRenderStatusClip TypicalClip() => new()
        {
            assets =
            [
                Landscape(Quality("480", 30), Quality("1080", 60), Quality("360", 30), Quality("720", 60)),
                Portrait(Quality("720", 30), Quality("1080", 60))
            ]
        };

        /// <summary>
        /// Qualities are named "{height}p{fps}" (portrait ones suffixed "-Portrait") and ordered landscape first, then by height
        /// and framerate descending, so the GUI/CLI quality lists read top-down from best to worst.
        /// </summary>
        [Fact]
        public void FromClip_NamesAndSortsQualities()
        {
            var qualities = VideoQualities.FromClip(TypicalClip());

            Assert.Equal(["1080p60", "720p60", "480p30", "360p30", "1080p60-Portrait", "720p30-Portrait"], qualities.Select(x => x.Name));
            Assert.Equal(qualities.Select(x => x.Name), qualities.Select(x => x.ToString()));
        }

        /// <summary>
        /// When Twitch omits width/height, the width is derived from the asset's aspect ratio; orientation, framerate, bitrate and
        /// URL are carried over from the GQL object, and only the tallest landscape quality is marked as source.
        /// </summary>
        [Fact]
        public void FromClip_BuildsQualityDetails()
        {
            var qualities = VideoQualities.FromClip(TypicalClip()).Qualities;

            var best = qualities[0];
            Assert.Equal(new Resolution(1920, 1080), best.Resolution);
            Assert.Equal(60, best.Framerate);
            Assert.Equal(1000, best.BitRate);
            Assert.Equal(best.Item.sourceURL, best.Path);
            Assert.Equal(VideoOrientation.Landscape, best.Orientation);
            Assert.True(best.IsSource);
            Assert.Single(qualities, x => x.IsSource);

            var portrait = qualities.Single(x => x.Name == "1080p60-Portrait");
            Assert.Equal(new Resolution(608, 1080), portrait.Resolution);
            Assert.Equal(VideoOrientation.Portrait, portrait.Orientation);
        }

        /// <summary>Explicit width/height from Twitch win over aspect-ratio guesses; with neither, only the height is known.</summary>
        [Fact]
        public void FromClip_PrefersExplicitDimensions_AndFallsBackToHeightOnly()
        {
            var clip = new ShareClipRenderStatusClip
            {
                assets = [new ShareClipRenderStatusAsset { aspectRatio = 0, thumbnailURL = "", videoQualities = [Quality("720", 30, 1000, 720), Quality("480", 30)] }]
            };

            var qualities = VideoQualities.FromClip(clip).Qualities;

            Assert.Equal(new Resolution(1000, 720), qualities[0].Resolution);
            Assert.Equal(new Resolution(480), qualities[1].Resolution);
        }

        /// <summary>
        /// Two qualities with the same name get "-1"/"-2" suffixes so neither is unreachable by name.
        /// Guards the duplicate counting in BuildQualityList.
        /// </summary>
        [Fact]
        public void FromClip_DisambiguatesDuplicateNames()
        {
            var clip = new ShareClipRenderStatusClip { assets = [Landscape(Quality("720", 30, bitrate: 2000), Quality("720", 30, bitrate: 1000))] };

            var qualities = VideoQualities.FromClip(clip);

            Assert.Equal(["720p30-2", "720p30-1"], qualities.Select(x => x.Name));
        }

        /// <summary>
        /// The user's quality string resolves by exact name (case/whitespace-insensitive), keywords (best/source/worst, optionally
        /// "portrait"), or a resolution/framerate pattern. Empty input means best. Unknown values return null so the caller can fall back.
        /// </summary>
        [Theory]
        [InlineData("1080p60", "1080p60")]
        [InlineData(" 720P60 ", "720p60")]
        [InlineData("480p30", "480p30")]
        [InlineData("1080p60-portrait", "1080p60-Portrait")]
        [InlineData(null, "1080p60")]
        [InlineData("", "1080p60")]
        [InlineData("Best", "1080p60")]
        [InlineData("source", "1080p60")]
        [InlineData("Worst", "360p30")]
        [InlineData("Best Portrait", "1080p60-Portrait")]
        [InlineData("Worst Portrait", "720p30-Portrait")]
        [InlineData("480", "480p30")]
        [InlineData("1080p", "1080p60")]
        [InlineData("854x480p30", null)] // Width is checked: the landscape 480p is 853 wide
        [InlineData("853x480p30", "480p30")]
        [InlineData("1440p60", null)]
        [InlineData("potato", null)]
        public void GetQuality_ResolvesUserQualityString(string? qualityString, string? expectedName)
        {
            var qualities = VideoQualities.FromClip(TypicalClip());

            Assert.Equal(expectedName, qualities.GetQuality(qualityString)?.Name);
        }

        /// <summary>
        /// Without a source flag (no landscape asset), best/worst fall back to the tallest/shortest landscape-shaped quality,
        /// and if no dimensions are known at all, to the first/last entry, so a download never fails just because metadata is sparse.
        /// </summary>
        [Fact]
        public void BestAndWorst_FallBackWhenMetadataIsSparse()
        {
            var noSource = new ClipVideoQualities([
                new ClipVideoQuality(Quality("720", 30), "a", new Resolution(1280, 720), false, VideoOrientation.Landscape),
                new ClipVideoQuality(Quality("480", 30), "b", new Resolution(854, 480), false, VideoOrientation.Landscape),
            ]);
            Assert.Equal("a", noSource.BestQuality()!.Name);
            Assert.Equal("b", noSource.WorstQuality()!.Name);

            var noDimensions = new ClipVideoQualities([
                new ClipVideoQuality(Quality("x", 0), "first", default, false, VideoOrientation.None),
                new ClipVideoQuality(Quality("y", 0), "last", default, false, VideoOrientation.None),
            ]);
            Assert.Equal("first", noDimensions.BestQuality()!.Name);
            Assert.Equal("first", noDimensions.WorstQuality()!.Name); // MinBy on equal heights returns the first
            Assert.Equal("first", noDimensions.GetQuality("best portrait")!.Name);
            Assert.Equal("first", noDimensions.GetQuality("worst portrait")!.Name);
        }

        /// <summary>A collection without a quality list returns null for best/worst instead of throwing.</summary>
        [Fact]
        public void BestAndWorst_ReturnNull_WhenQualitiesNull()
        {
            var qualities = new ClipVideoQualities(null!);

            Assert.Null(qualities.BestQuality());
            Assert.Null(qualities.WorstQuality());
        }
    }
}
