using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    public class StringBuilderTrimEndTests
    {
        // Any trailing run of the given chars is removed (including CRLF and the whole builder), chars not in the set stop the trimming, and a
        // set with no match leaves the builder untouched.
        [Theory]
        [InlineData("Foo", "o", "F")]
        [InlineData("Foo\r\n", "\r\n", "Foo")]
        [InlineData("oo", "o", "")]
        [InlineData("Foo", "L", "Foo")]
        [InlineData("Foo", "oL", "F")]
        public void TrimsCorrectCharacters(string baseString, string trimChars, string expectedResult)
        {
            var sb = new StringBuilder(baseString);

            StringBuilderExtensions.TrimEnd(sb, trimChars);

            Assert.Equal(expectedResult, sb.ToString());
        }
    }
}