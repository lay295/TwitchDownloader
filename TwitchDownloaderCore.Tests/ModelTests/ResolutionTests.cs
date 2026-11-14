using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // Tests the Resolution record struct, the common size type for VOD and clip qualities used by quality selection and sorting.
    public class ResolutionTests
    {
        /// <summary>The height-only constructor leaves width at 0 so HasWidth is false; clip qualities sometimes only know their height.</summary>
        [Fact]
        public void HeightOnlyConstructor_HasNoWidth()
        {
            var resolution = new Resolution(720);

            Assert.Equal(0u, resolution.Width);
            Assert.Equal(720u, resolution.Height);
            Assert.False(resolution.HasWidth);
            Assert.True(new Resolution(1280, 720).HasWidth);
        }

        /// <summary>Pixels is width * height; it is the primary key when picking the best/worst quality.</summary>
        [Fact]
        public void Pixels_IsWidthTimesHeight()
        {
            Assert.Equal(2_073_600u, new Resolution(1920, 1080).Pixels);
        }

        /// <summary>ToString prints "WxH", the format shown in quality lists and logs.</summary>
        [Fact]
        public void ToString_IsWidthByHeight()
        {
            Assert.Equal("1920x1080", new Resolution(1920, 1080).ToString());
        }

        /// <summary>
        /// Implicit conversions to/from tuples and M3U8 StreamResolution must keep width and height in order;
        /// a swap would turn every landscape stream into a portrait one.
        /// </summary>
        [Fact]
        public void ImplicitConversions_PreserveWidthAndHeight()
        {
            Resolution fromTuple = (1280u, 720u);
            Resolution fromStream = new M3U8.Stream.ExtStreamInfo.StreamResolution(854, 480);
            M3U8.Stream.ExtStreamInfo.StreamResolution toStream = new Resolution(640, 360);

            Assert.Equal(new Resolution(1280, 720), fromTuple);
            Assert.Equal(new Resolution(854, 480), fromStream);
            Assert.Equal(new M3U8.Stream.ExtStreamInfo.StreamResolution(640, 360), toStream);
        }
    }
}
