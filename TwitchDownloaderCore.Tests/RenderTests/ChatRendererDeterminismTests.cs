using System.Runtime.CompilerServices;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    // Tests that ChatRenderer picks the same default username color and default avatar for a user on every render.
    // Users without a chat color get one derived from their name; this used string.GetHashCode(), which .NET randomizes per process,
    // so re-rendering the same chat produced different colors/avatars each time (and broke the before/after render preview in CI).
    public class ChatRendererDeterminismTests
    {
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "StableIndex")]
        private static extern int StableIndex(ChatRenderer? @null, string text, int length);

        /// <summary>
        /// The index must be a pure function of the name, identical in every process and on every platform. These values are pinned:
        /// a per-process randomized hash could not keep matching them, so a regression back to GetHashCode() fails here.
        /// </summary>
        [Theory]
        [InlineData("viewer_one", 15, 9)]
        [InlineData("TestStreamer", 15, 4)]
        [InlineData("PrimeUser", 15, 9)]
        [InlineData("日本語ユーザー", 15, 7)]
        [InlineData("", 15, 1)]
        [InlineData("viewer_one", 6, 0)]
        [InlineData("TestStreamer", 6, 4)]
        [InlineData("PrimeUser", 6, 3)]
        public void StableIndex_IsFixedForAName(string displayName, int length, int expected)
        {
            Assert.Equal(expected, StableIndex(null, displayName, length));
        }

        /// <summary>
        /// The index must always be a valid array index, including for names whose hash would be int.MinValue under the old
        /// Math.Abs(hash) % length scheme (Math.Abs throws for int.MinValue).
        /// </summary>
        [Fact]
        public void StableIndex_IsAlwaysInRange()
        {
            for (var i = 0; i < 10_000; i++)
            {
                var index = StableIndex(null, $"user{i}é\U0001F600", 15);
                Assert.InRange(index, 0, 14);
            }
        }
    }
}
