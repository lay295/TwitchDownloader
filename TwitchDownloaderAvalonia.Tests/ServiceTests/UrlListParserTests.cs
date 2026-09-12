using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class UrlListParserTests
    {
        [Fact]
        public void EmptyTextHasNoEntries()
        {
            var result = UrlListParser.Parse("  \n  \n");

            Assert.Empty(result.Entries);
            Assert.Empty(result.Invalid);
        }

        [Fact]
        public void ParsesVodIdAndUrl()
        {
            var result = UrlListParser.Parse("""
                6834869128
                https://www.twitch.tv/videos/11987163407
                """);

            Assert.Empty(result.Invalid);
            Assert.Equal(2, result.Entries.Count);
            Assert.Equal("6834869128", result.Entries[0].Id);
            Assert.False(result.Entries[0].IsClip);
            Assert.Equal("11987163407", result.Entries[1].Id);
            Assert.False(result.Entries[1].IsClip);
        }

        [Fact]
        public void ParsesClipUrlAndSlug()
        {
            var result = UrlListParser.Parse("""
                SpineyPieTwitchRPGNurturing
                https://clips.twitch.tv/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf
                """);

            Assert.Empty(result.Invalid);
            Assert.Equal(2, result.Entries.Count);
            Assert.True(result.Entries[0].IsClip);
            Assert.Equal("SpineyPieTwitchRPGNurturing", result.Entries[0].Id);
            Assert.True(result.Entries[1].IsClip);
            Assert.Equal("FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", result.Entries[1].Id);
        }

        [Fact]
        public void MixedListKeepsOrderAndSkipsBlankLines()
        {
            var result = UrlListParser.Parse("""

                https://www.twitch.tv/videos/41546181

                https://clips.twitch.tv/SpineyPieTwitchRPGNurturing

                """);

            Assert.Empty(result.Invalid);
            Assert.Equal(2, result.Entries.Count);
            Assert.Equal("41546181", result.Entries[0].Id);
            Assert.False(result.Entries[0].IsClip);
            Assert.Equal("SpineyPieTwitchRPGNurturing", result.Entries[1].Id);
            Assert.True(result.Entries[1].IsClip);
        }

        [Fact]
        public void InvalidLinesAreCollectedWithoutDroppingValidOnes()
        {
            var result = UrlListParser.Parse("""
                not a twitch url
                https://www.twitch.tv/videos/982306410
                https://example.com/watch
                """);

            Assert.Equal(["not a twitch url", "https://example.com/watch"], result.Invalid);
            Assert.Equal("982306410", Assert.Single(result.Entries).Id);
        }

        [Fact]
        public void NullTextIsEmpty()
        {
            var result = UrlListParser.Parse(null);

            Assert.Empty(result.Entries);
            Assert.Empty(result.Invalid);
        }
    }
}
