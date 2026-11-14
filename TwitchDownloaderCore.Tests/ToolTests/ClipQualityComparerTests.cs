using TwitchDownloaderCore.Tools;
using ClipVideoQuality = TwitchDownloaderCore.TwitchObjects.Gql.ClipVideoQuality;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests ClipQualityComparer, which orders the legacy clip token qualities best-first for the GUI quality list.
    public class ClipQualityComparerTests
    {
        private static ClipVideoQuality Q(string quality, decimal frameRate = 30) => new() { quality = quality, frameRate = frameRate };

        /// <summary>
        /// Sorting puts numeric qualities first by height descending, then framerate descending, and non-numeric labels last
        /// (in reverse ordinal order). Guards the "first entry is the best quality" assumption.
        /// </summary>
        [Fact]
        public void SortsBestFirst()
        {
            var qualities = new[] { Q("480"), Q("abc"), Q("720", 30), Q("1080", 60), Q("720", 60), Q("xyz") };

            Array.Sort(qualities, new ClipQualityComparer());

            Assert.Equal(["1080p60", "720p60", "720p30", "480p30", "xyzp30", "abcp30"], qualities.Select(x => $"{x.quality}p{x.frameRate}"));
        }

        /// <summary>Nulls sort first and equal qualities compare as 0, as required by the IComparer contract.</summary>
        [Fact]
        public void HandlesNullsAndEquality()
        {
            var comparer = new ClipQualityComparer();

            Assert.Equal(0, comparer.Compare(null, null));
            Assert.Equal(-1, comparer.Compare(null, Q("720")));
            Assert.Equal(1, comparer.Compare(Q("720"), null));
            Assert.Equal(0, comparer.Compare(Q("720", 60), Q("720", 60)));
        }
    }
}
