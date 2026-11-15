using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests CommentIdEqualityComparer, used to de-duplicate comments fetched by overlapping parallel chat download segments.
    public class CommentIdEqualityComparerTests
    {
        private static Comment C(string id, double offset = 0) => new() { _id = id, content_offset_seconds = offset };

        /// <summary>
        /// Comments are equal exactly when their ids match, regardless of other fields, and equal comments hash alike;
        /// so Distinct drops the duplicate copy of a comment downloaded by two segments.
        /// </summary>
        [Fact]
        public void ComparesById()
        {
            var comparer = new CommentIdEqualityComparer();

            Assert.True(comparer.Equals(C("a", 1), C("a", 2)));
            Assert.False(comparer.Equals(C("a"), C("b")));
            Assert.Equal(comparer.GetHashCode(C("a", 1)), comparer.GetHashCode(C("a", 2)));
            Assert.Equal(["a", "b"], new[] { C("a"), C("b"), C("a", 5) }.Distinct(comparer).Select(x => x._id));
        }

        /// <summary>Null handling follows the IEqualityComparer contract: two nulls are equal, null and non-null are not.</summary>
        [Fact]
        public void HandlesNulls()
        {
            var comparer = new CommentIdEqualityComparer();

            Assert.True(comparer.Equals(null, null));
            Assert.False(comparer.Equals(null, C("a")));
            Assert.False(comparer.Equals(C("a"), null));
        }
    }
}
