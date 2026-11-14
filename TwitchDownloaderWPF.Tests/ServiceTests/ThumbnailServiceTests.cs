using TwitchDownloaderWPF.Services;

namespace TwitchDownloaderWPF.Tests.ServiceTests
{
    // Tests ThumbnailService, used by every page and the queue to turn Twitch thumbnail URLs into images. Twitch returns null/empty
    // thumbnail URLs for some VODs and clips, and callers rely on TryGetThumb returning false (then falling back to the "missing" image)
    // instead of throwing. Only the paths that never start a network download are tested.
    public class ThumbnailServiceTests
    {
        // Verifies null, empty and whitespace URLs return false with a null image.
        // Why: these are what Twitch sends for deleted/processing videos; an exception here would abort loading the whole video info panel.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryGetThumb_ReturnsFalseForMissingUrl(string? url)
        {
            Assert.False(ThumbnailService.TryGetThumb(url!, out var thumbnail));
            Assert.Null(thumbnail);
        }

        // Verifies a string that is not a URI returns false instead of throwing UriFormatException.
        // Why: TryGetThumb is the "never throws" variant; callers do not wrap it in try/catch.
        [Fact]
        public void TryGetThumb_ReturnsFalseForMalformedUrl()
        {
            Assert.False(ThumbnailService.TryGetThumb("not a url", out var thumbnail));
            Assert.Null(thumbnail);
        }

        // Verifies GetThumb rejects null with ArgumentNullException, as documented.
        // Why: callers that use GetThumb directly depend on the documented exception type, not a NullReferenceException from deeper inside WPF.
        [Fact]
        public void GetThumb_ThrowsForNull()
        {
            Assert.Throws<ArgumentNullException>(() => ThumbnailService.GetThumb(null!));
        }

        // Verifies the fallback "thumbnail missing" URL is an absolute https URL.
        // Why: every caller falls back to this URL when the real thumbnail fails; if it were not a valid absolute URI the fallback
        // would fail too and the GUI would show no image at all.
        [Fact]
        public void MissingThumbnailUrl_IsAbsoluteHttps()
        {
            Assert.True(Uri.TryCreate(ThumbnailService.THUMBNAIL_MISSING_URL, UriKind.Absolute, out var uri));
            Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        }
    }
}
