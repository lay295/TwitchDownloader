using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class AvailableCulturesTests
    {
        [Theory]
        [InlineData("en-US", "en-US")]
        [InlineData("de-AT", "de-DE")]
        [InlineData("zh-CN", "zh-CN")]
        [InlineData("zh-TW", "zh-TW")]
        [InlineData("zh-Hans", "zh-CN")]
        [InlineData("zh-Hant", "zh-TW")]
        [InlineData("zh-HK", "zh-TW")]
        [InlineData("zh-MO", "zh-TW")]
        [InlineData("zh", "zh-CN")]
        [InlineData("", "en-US")]
        [InlineData(null, "en-US")]
        public void ResolvesExpectedCulture(string? culture, string expected)
        {
            Assert.Equal(expected, AvailableCultures.Resolve(culture));
        }
    }
}
