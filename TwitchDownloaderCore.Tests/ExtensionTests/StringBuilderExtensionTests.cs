using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    public class StringBuilderExtensionTests
    {
        [Theory]
        [InlineData("Foo", "o", "F")]
        [InlineData("Foo\r\n", "\r\n", "Foo")]
        [InlineData("oo", "o", "")]
        [InlineData("Foo", "L", "Foo")]
        [InlineData("Foo", "oL", "F")]
        public void CorrectlyTrimsCharacters(string baseString, string trimChars, string expectedResult)
        {
            var sb = new StringBuilder(baseString);

            sb.TrimEnd(trimChars);

            Assert.Equal(expectedResult, sb.ToString());
        }
    }
}