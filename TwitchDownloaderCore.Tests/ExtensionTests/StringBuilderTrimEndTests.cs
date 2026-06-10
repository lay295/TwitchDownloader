using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // ReSharper disable InvokeAsExtensionMember
    public class StringBuilderTrimEndTests
    {
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