using System.Buffers;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable StringLiteralTypo InvokeAsExtensionMember
    public class StringReplaceAnyTests
    {
        // ReplaceAny must be equivalent to chaining string.Replace once per char, which is the behaviour it was written to speed up.
        [Fact]
        public void MatchesMultipleStringReplaceUses()
        {
            const string STRING = "SORRY FOR TRAFFIC NaM.";
            const string OLD_CHARS = "FRM";
            const char NEW_CHAR = 'L';

            var replaceResult1 = STRING.Replace(OLD_CHARS[0], NEW_CHAR);
            var replaceResult2 = replaceResult1.Replace(OLD_CHARS[1], NEW_CHAR);
            var replaceResult3 = replaceResult2.Replace(OLD_CHARS[2], NEW_CHAR);

            var replaceAnyResult = StringExtensions.ReplaceAny(STRING, SearchValues.Create(OLD_CHARS), NEW_CHAR);

            Assert.Equal(replaceResult3, replaceAnyResult);
        }

        // Every occurrence of every listed char is replaced (case-sensitively) and all other chars are kept, pinned against a literal expected string.
        [Fact]
        public void CorrectlyReplacesAnyCharacter()
        {
            const string STRING = "SORRY FOR TRAFFIC NaM.";
            const string OLD_CHARS = "FRM";
            const char NEW_CHAR = 'L';
            const string EXPECTED = "SOLLY LOL TLALLIC NaL.";

            var result = StringExtensions.ReplaceAny(STRING, SearchValues.Create(OLD_CHARS), NEW_CHAR);

            Assert.Equal(EXPECTED, result);
        }

        // An empty input is returned as the same instance: no allocation, no exception.
        [Fact]
        public void ReturnsOriginalString_WhenEmpty()
        {
            const string STRING = "";
            const string OLD_CHARS = "";
            const char NEW_CHAR = 'L';

            var result = StringExtensions.ReplaceAny(STRING, SearchValues.Create(OLD_CHARS), NEW_CHAR);

            Assert.Same(STRING, result);
        }

        // When none of the chars occur the original instance is returned (callers rely on this to avoid copying every file name).
        [Fact]
        public void ReturnsOriginalString_WhenOldCharsNotPresent()
        {
            const string STRING = "SORRY FOR TRAFFIC NaM.";
            const string OLD_CHARS = "PogU";
            const char NEW_CHAR = 'L';

            var result = StringExtensions.ReplaceAny(STRING, SearchValues.Create(OLD_CHARS), NEW_CHAR);

            Assert.Same(STRING, result);
        }
    }
}