using TwitchDownloaderAvalonia.Converters;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class LocalizationFormatTests
    {
        [Fact]
        public void FormatReturnsTemplateWhenPlaceholderIsInvalid()
        {
            var formatted = LocalizationService.Format("Hello {0", "test.key", "world");

            Assert.Equal("Hello {0", formatted);
        }

        [Fact]
        public void FormatSubstitutesArguments()
        {
            var formatted = LocalizationService.Format("Hello {0}", "test.key", "world");

            Assert.Equal("Hello world", formatted);
        }
    }

    public class QualityLabelsTests
    {
        [Fact]
        public void UnknownQualityKeepsOriginalName()
        {
            Assert.Equal("1080p60", QualityLabels.Get("1080p60"));
        }
    }
}
