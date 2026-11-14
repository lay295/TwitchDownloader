using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests ClipVideoQualityComparer, which VideoQualities.FromClip uses to sort each clip asset's qualities best-first
    // (and so decides which quality is flagged as source).
    public class ClipVideoQualityComparerTests
    {
        private static ShareClipRenderStatusVideoQuality Q(string quality, decimal frameRate = 30) => new() { quality = quality, frameRate = frameRate };

        /// <summary>
        /// Numeric qualities come first by height descending, then framerate descending; non-numeric labels go last
        /// (in reverse ordinal order) so a junk label can never be picked as the best quality.
        /// </summary>
        [Fact]
        public void SortsBestFirst()
        {
            var qualities = new[] { Q("480"), Q("abc"), Q("720", 30), Q("1080", 60), Q("720", 60), Q("xyz") };

            Array.Sort(qualities, new ClipVideoQualityComparer());

            Assert.Equal(["1080p60", "720p60", "720p30", "480p30", "xyzp30", "abcp30"], qualities.Select(x => $"{x.quality}p{x.frameRate}"));
        }

        /// <summary>Nulls sort first and equal qualities compare as 0, as required by the IComparer contract.</summary>
        [Fact]
        public void HandlesNullsAndEquality()
        {
            var comparer = new ClipVideoQualityComparer();

            Assert.Equal(0, comparer.Compare(null, null));
            Assert.Equal(-1, comparer.Compare(null, Q("720")));
            Assert.Equal(1, comparer.Compare(Q("720"), null));
            Assert.Equal(0, comparer.Compare(Q("720", 60), Q("720", 60)));
        }
    }
}
