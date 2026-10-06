using TwitchDownloaderAvalonia.Converters;
using TwitchDownloaderAvalonia.Models;
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
            Assert.Equal("1080p60", QualityLabels.Get(TestLocalization.Instance, "1080p60"));
        }

        [Fact]
        public void NamedQualitiesMapToLocalizationKeys()
        {
            var loc = TestLocalization.Instance;

            Assert.Equal(loc.Get("quality.source"), QualityLabels.Get(loc, QualityNames.SOURCE));
            Assert.Equal(loc.Get("quality.source_portrait"), QualityLabels.Get(loc, QualityNames.SOURCE_PORTRAIT));
            Assert.Equal(loc.Get("quality.worst"), QualityLabels.Get(loc, QualityNames.WORST));
            Assert.Equal(loc.Get("quality.worst_portrait"), QualityLabels.Get(loc, QualityNames.WORST_PORTRAIT));
            Assert.Equal(loc.Get("quality.audio_only"), QualityLabels.Get(loc, QualityNames.AUDIO_ONLY));
        }
    }
}
