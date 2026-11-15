using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests CommentOffsetComparer, which orders downloaded/updated chat comments by stream offset (then creation time).
    // Wrong ordering shows up as out-of-order messages in rendered chats.
    public class CommentOffsetComparerTests
    {
        private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static Comment C(string id, double offset, int createdSecondsAfterT0 = 0) =>
            new() { _id = id, content_offset_seconds = offset, created_at = T0.AddSeconds(createdSecondsAfterT0) };

        /// <summary>Comments sort by offset first, and by created_at when offsets tie (common, as offsets are whole seconds in old chats).</summary>
        [Fact]
        public void SortsByOffsetThenCreationDate()
        {
            var comments = new List<Comment> { C("c", 5, 0), C("b", 2, 9), C("a2", 1, 2), C("a1", 1, 1) };

            comments.Sort(new CommentOffsetComparer());

            Assert.Equal(["a1", "a2", "b", "c"], comments.Select(x => x._id));
        }

        /// <summary>
        /// Two comments with identical offset and creation date compare as 1, never 0, on purpose: the code notes that returning 0
        /// made the sorter discard one of them. Both comments must survive sorting.
        /// </summary>
        [Fact]
        public void NeverReportsIdenticalCommentsAsEqual()
        {
            var comparer = new CommentOffsetComparer();
            var a = C("a", 3, 3);
            var b = C("b", 3, 3);

            Assert.Equal(1, comparer.Compare(a, b));
            var sorted = new SortedSet<Comment>([a, b], comparer);
            Assert.Equal(2, sorted.Count);
        }

        /// <summary>NaN offsets (corrupt chat data) sort before real offsets, like double.CompareTo, and nulls sort first.</summary>
        [Fact]
        public void HandlesNaNAndNulls()
        {
            var comparer = new CommentOffsetComparer();

            Assert.Equal(-1, comparer.Compare(C("nan", double.NaN), C("x", 1)));
            Assert.Equal(1, comparer.Compare(C("x", 1), C("nan", double.NaN)));
            Assert.Equal(0, comparer.Compare(C("nan", double.NaN), C("nan2", double.NaN)));
            Assert.Equal(0, comparer.Compare(null, null));
            Assert.Equal(-1, comparer.Compare(null, C("x", 1)));
            Assert.Equal(1, comparer.Compare(C("x", 1), null));
        }
    }
}
