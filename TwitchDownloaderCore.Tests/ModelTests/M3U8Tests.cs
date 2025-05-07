using System.Globalization;
using System.Text;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // ReSharper disable StringLiteralTypo
    public class M3U8Tests
    {
        [Theory]
        [InlineData(false, "en-US")]
        [InlineData(true, "en-US")]
        [InlineData(false, "ru-RU")]
        [InlineData(true, "ru-RU")]
        public void CorrectlyParsesTwitchM3U8OfTransportStreams(bool useStream, string culture)
        {
            const string EXAMPLE_M3U8_TWITCH =
                "#EXTM3U" +
                "\n#EXT-X-VERSION:3" +
                "\n#EXT-X-TARGETDURATION:10" +
                "\n#ID3-EQUIV-TDTG:2023-09-23T17:37:06" +
                "\n#EXT-X-PLAYLIST-TYPE:EVENT" +
                "\n#EXT-X-MEDIA-SEQUENCE:0" +
                "\n#EXT-X-TWITCH-ELAPSED-SECS:0.000" +
                "\n#EXT-X-TWITCH-TOTAL-SECS:500.000" +
                "\n#EXTINF:10.000,\n0.ts\n#EXTINF:10.000,\n1.ts\n#EXTINF:10.000,\n2.ts\n#EXTINF:10.000,\n3.ts\n#EXTINF:10.000,\n4.ts\n#EXTINF:10.000,\n5.ts\n#EXTINF:10.000,\n6.ts\n#EXTINF:10.000,\n7.ts" +
                "\n#EXTINF:10.000,\n8.ts\n#EXTINF:10.000,\n9.ts\n#EXTINF:10.000,\n10.ts\n#EXTINF:10.000,\n11.ts\n#EXTINF:10.000,\n12.ts\n#EXTINF:10.000,\n13.ts\n#EXTINF:10.000,\n14.ts\n#EXTINF:10.000,\n15.ts" +
                "\n#EXTINF:10.000,\n16.ts\n#EXTINF:10.000,\n17.ts\n#EXTINF:10.000,\n18.ts\n#EXTINF:10.000,\n19.ts\n#EXTINF:10.000,\n20.ts\n#EXTINF:10.000,\n21.ts\n#EXTINF:10.000,\n22.ts\n#EXTINF:10.000,\n23.ts" +
                "\n#EXTINF:10.000,\n24.ts\n#EXTINF:10.000,\n25.ts\n#EXTINF:10.000,\n26.ts\n#EXTINF:10.000,\n27.ts\n#EXTINF:10.000,\n28.ts\n#EXTINF:10.000,\n29.ts\n#EXTINF:10.000,\n30.ts\n#EXTINF:10.000,\n31.ts" +
                "\n#EXTINF:10.000,\n32.ts\n#EXTINF:10.000,\n33.ts\n#EXTINF:10.000,\n34.ts\n#EXTINF:10.000,\n35.ts\n#EXTINF:10.000,\n36.ts\n#EXTINF:10.000,\n37.ts\n#EXTINF:10.000,\n38.ts\n#EXTINF:10.000,\n39.ts" +
                "\n#EXTINF:10.000,\n40.ts\n#EXTINF:10.000,\n41.ts\n#EXTINF:10.000,\n42.ts\n#EXTINF:10.000,\n43.ts\n#EXTINF:10.000,\n44.ts\n#EXTINF:10.000,\n45.ts\n#EXTINF:10.000,\n46.ts\n#EXTINF:10.000,\n47.ts" +
                "\n#EXTINF:10.000,\n48.ts\n#EXTINF:10.000,\n49.ts\n#EXT-X-ENDLIST";

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            M3U8 m3u8;
            if (useStream)
            {
                var bytes = Encoding.Unicode.GetBytes(EXAMPLE_M3U8_TWITCH);
                using var ms = new MemoryStream(bytes);
                m3u8 = M3U8.Parse(ms, Encoding.Unicode);
            }
            else
            {
                m3u8 = M3U8.Parse(EXAMPLE_M3U8_TWITCH);
            }

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(3u, m3u8.FileMetadata.Version);
            Assert.Equal(10u, m3u8.FileMetadata.StreamTargetDuration);
            Assert.Equal("2023-09-23T17:37:06", m3u8.FileMetadata.UnparsedValues.FirstOrDefault(x => x.Key == "#ID3-EQUIV-TDTG:").Value);
            Assert.Equal(M3U8.Metadata.PlaylistType.Event, m3u8.FileMetadata.Type);
            Assert.Equal(0u, m3u8.FileMetadata.MediaSequence);
            Assert.Equal(0m, m3u8.FileMetadata.TwitchElapsedSeconds);
            Assert.Equal(500m, m3u8.FileMetadata.TwitchTotalSeconds);

            Assert.Equal(50, m3u8.Streams.Length);
            for (var i = 0; i < m3u8.Streams.Length; i++)
            {
                var stream = m3u8.Streams[i];
                Assert.Equal(10, stream.PartInfo.Duration);
                Assert.False(stream.PartInfo.Live);
                Assert.Equal($"{i}.ts", stream.Path);
            }
        }

        [Theory]
        [InlineData(false, "en-US")]
        [InlineData(true, "en-US")]
        [InlineData(false, "ru-RU")]
        [InlineData(true, "ru-RU")]
        public void CorrectlyParsesTwitchM3U8OfMp4s(bool useStream, string culture)
        {
            const string EXAMPLE_M3U8_TWITCH =
                "#EXTM3U" +
                "\n#EXT-X-VERSION:6" +
                "\n#EXT-X-TARGETDURATION:10" +
                "\n#ID3-EQUIV-TDTG:2024-12-08T00:12:24" +
                "\n#EXT-X-PLAYLIST-TYPE:EVENT" +
                "\n#EXT-X-MEDIA-SEQUENCE:0" +
                "\n#EXT-X-TWITCH-ELAPSED-SECS:0.000" +
                "\n#EXT-X-TWITCH-TOTAL-SECS:1137.134" +
                "\n#EXT-X-MAP:URI=\"init-0.mp4\"" +
                "\n#EXTINF:10.000,\n0.mp4\n#EXTINF:10.000,\n1.mp4\n#EXTINF:10.000,\n2.mp4\n#EXTINF:10.000,\n3.mp4\n#EXTINF:10.000,\n4.mp4\n#EXTINF:10.000,\n5.mp4\n#EXTINF:10.000,\n6.mp4\n#EXTINF:10.000,\n7.mp4" +
                "\n#EXTINF:10.000,\n8.mp4\n#EXTINF:10.000,\n9.mp4\n#EXTINF:10.000,\n10.mp4\n#EXTINF:10.000,\n11.mp4\n#EXTINF:10.000,\n12.mp4\n#EXTINF:10.000,\n13.mp4\n#EXTINF:10.000,\n14.mp4\n#EXTINF:10.000," +
                "\n15.mp4\n#EXTINF:10.000,\n16.mp4\n#EXTINF:10.000,\n17.mp4\n#EXTINF:10.000,\n18.mp4\n#EXTINF:10.000,\n19.mp4\n#EXTINF:10.000,\n20.mp4\n#EXTINF:10.000,\n21.mp4\n#EXTINF:10.000,\n22.mp4" +
                "\n#EXTINF:10.000,\n23.mp4\n#EXTINF:10.000,\n24.mp4\n#EXTINF:10.000,\n25.mp4\n#EXTINF:10.000,\n26.mp4\n#EXTINF:10.000,\n27.mp4\n#EXTINF:10.000,\n28.mp4\n#EXTINF:10.000,\n29.mp4\n#EXTINF:10.000," +
                "\n30.mp4\n#EXTINF:10.000,\n31.mp4\n#EXTINF:10.000,\n32.mp4\n#EXTINF:10.000,\n33.mp4\n#EXTINF:10.000,\n34.mp4\n#EXTINF:10.000,\n35.mp4\n#EXTINF:10.000,\n36.mp4\n#EXTINF:10.000,\n37.mp4" +
                "\n#EXTINF:10.000,\n38.mp4\n#EXTINF:10.000,\n39.mp4\n#EXTINF:10.000,\n40.mp4\n#EXTINF:10.000,\n41.mp4\n#EXTINF:10.000,\n42.mp4\n#EXTINF:10.000,\n43.mp4\n#EXTINF:10.000,\n44.mp4\n#EXTINF:10.000," +
                "\n45.mp4\n#EXTINF:10.000,\n46.mp4\n#EXTINF:10.000,\n47.mp4\n#EXTINF:10.000,\n48.mp4\n#EXTINF:10.000,\n49.mp4\n#EXTINF:10.000,\n50.mp4\n#EXTINF:10.000,\n51.mp4\n#EXTINF:10.000,\n52.mp4" +
                "\n#EXTINF:10.000,\n53.mp4\n#EXTINF:10.000,\n54.mp4\n#EXTINF:10.000,\n55.mp4\n#EXTINF:10.000,\n56.mp4\n#EXTINF:10.000,\n57.mp4\n#EXTINF:10.000,\n58.mp4\n#EXTINF:10.000,\n59.mp4\n#EXTINF:10.000," +
                "\n60.mp4\n#EXTINF:10.000,\n61.mp4\n#EXTINF:10.000,\n62.mp4\n#EXTINF:10.000,\n63.mp4\n#EXTINF:10.000,\n64.mp4\n#EXTINF:10.000,\n65.mp4\n#EXTINF:10.000,\n66.mp4\n#EXTINF:10.000,\n67.mp4" +
                "\n#EXTINF:10.000,\n68.mp4\n#EXTINF:10.000,\n69.mp4\n#EXTINF:10.000,\n70.mp4\n#EXTINF:10.000,\n71.mp4\n#EXTINF:10.000,\n72.mp4\n#EXTINF:10.000,\n73.mp4\n#EXTINF:10.000,\n74.mp4\n#EXTINF:10.000," +
                "\n75.mp4\n#EXTINF:10.000,\n76.mp4\n#EXTINF:10.000,\n77.mp4\n#EXTINF:10.000,\n78.mp4\n#EXTINF:10.000,\n79.mp4\n#EXTINF:10.000,\n80.mp4\n#EXTINF:10.000,\n81.mp4\n#EXTINF:10.000,\n82.mp4" +
                "\n#EXTINF:10.000,\n83.mp4\n#EXTINF:10.000,\n84.mp4\n#EXTINF:10.000,\n85.mp4\n#EXTINF:10.000,\n86.mp4\n#EXTINF:10.000,\n87.mp4\n#EXTINF:10.000,\n88.mp4\n#EXTINF:10.000,\n89.mp4\n#EXTINF:10.000," +
                "\n90.mp4\n#EXTINF:10.000,\n91.mp4\n#EXTINF:10.000,\n92.mp4\n#EXTINF:10.000,\n93.mp4\n#EXTINF:10.000,\n94.mp4\n#EXTINF:10.000,\n95.mp4\n#EXTINF:10.000,\n96.mp4\n#EXTINF:10.000,\n97.mp4" +
                "\n#EXTINF:10.000,\n98.mp4\n#EXTINF:10.000,\n99.mp4\n#EXTINF:10.000,\n100.mp4\n#EXTINF:10.000,\n101.mp4\n#EXTINF:10.000,\n102.mp4\n#EXTINF:10.000,\n103.mp4\n#EXTINF:10.000,\n104.mp4" +
                "\n#EXTINF:10.000,\n105.mp4\n#EXTINF:10.000,\n106.mp4\n#EXTINF:10.000,\n107.mp4\n#EXTINF:10.000,\n108.mp4\n#EXTINF:10.000,\n109.mp4\n#EXTINF:10.000,\n110.mp4\n#EXTINF:10.000,\n111.mp4" +
                "\n#EXTINF:10.000,\n112.mp4\n#EXTINF:7.134,\n113.mp4\n#EXT-X-ENDLIST";

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            M3U8 m3u8;
            if (useStream)
            {
                var bytes = Encoding.Unicode.GetBytes(EXAMPLE_M3U8_TWITCH);
                using var ms = new MemoryStream(bytes);
                m3u8 = M3U8.Parse(ms, Encoding.Unicode);
            }
            else
            {
                m3u8 = M3U8.Parse(EXAMPLE_M3U8_TWITCH);
            }

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(6u, m3u8.FileMetadata.Version);
            Assert.Equal(10u, m3u8.FileMetadata.StreamTargetDuration);
            Assert.Equal("2024-12-08T00:12:24", m3u8.FileMetadata.UnparsedValues.FirstOrDefault(x => x.Key == "#ID3-EQUIV-TDTG:").Value);
            Assert.Equal(M3U8.Metadata.PlaylistType.Event, m3u8.FileMetadata.Type);
            Assert.Equal(0u, m3u8.FileMetadata.MediaSequence);
            Assert.Equal("init-0.mp4", m3u8.FileMetadata.Map.Uri);
            Assert.Equal(default, m3u8.FileMetadata.Map.ByteRange);
            Assert.Equal(0m, m3u8.FileMetadata.TwitchElapsedSeconds);
            Assert.Equal(1137.134m, m3u8.FileMetadata.TwitchTotalSeconds);

            Assert.Equal(114, m3u8.Streams.Length);

            var duration = 1137.134m;
            for (var i = 0; i < m3u8.Streams.Length; i++)
            {
                var stream = m3u8.Streams[i];
                Assert.Equal(duration > 10 ? 10 : duration, stream.PartInfo.Duration);
                Assert.False(stream.PartInfo.Live);
                Assert.Equal($"{i}.mp4", stream.Path);

                duration -= 10;
            }
        }

        [Theory]
        [InlineData(false, "en-US")]
        [InlineData(true, "en-US")]
        [InlineData(false, "ru-RU")]
        [InlineData(true, "ru-RU")]
        public void CorrectlyParsesTwitchM3U8OfLiveStreams(bool useStream, string culture)
        {
            const string EXAMPLE_M3U8_TWITCH =
                "#EXTM3U" +
                "\n#EXT-X-VERSION:3" +
                "\n#EXT-X-TARGETDURATION:5" +
                "\n#EXT-X-MEDIA-SEQUENCE:4815" +
                "\n#EXT-X-TWITCH-LIVE-SEQUENCE:4997" +
                "\n#EXT-X-TWITCH-ELAPSED-SECS:9994.338" +
                "\n#EXT-X-TWITCH-TOTAL-SECS:10028.338" +
                "\n#EXT-X-DATERANGE:ID=\"playlist-creation-1694908286\",CLASS=\"timestamp\",START-DATE=\"2023-09-16T23:51:26.423Z\",END-ON-NEXT=YES,X-SERVER-TIME=\"1694908286.42\"" +
                "\n#EXT-X-DATERANGE:ID=\"playlist-session-1694908286\",CLASS=\"twitch-session\",START-DATE=\"2023-09-16T23:51:26.423Z\",END-ON-NEXT=YES,X-TV-TWITCH-SESSIONID=\"1234567890\"" +
                "\n#EXT-X-DATERANGE:ID=\"1234567890\",CLASS=\"twitch-assignment\",START-DATE=\"2023-09-17T02:27:42.242Z\",END-ON-NEXT=YES,X-TV-TWITCH-SERVING-ID=\"1234567890\",X-TV-TWITCH-NODE=\"video-edge-foo.bar\",X-TV-TWITCH-CLUSTER=\"foo\"" +
                "\n#EXT-X-DATERANGE:ID=\"source-1694916060\",CLASS=\"twitch-stream-source\",START-DATE=\"2023-09-17T02:01:00.242Z\",END-ON-NEXT=YES,X-TV-TWITCH-STREAM-SOURCE=\"live\"" +
                "\n#EXT-X-DATERANGE:ID=\"trigger-1694908279\",CLASS=\"twitch-trigger\",START-DATE=\"2023-09-16T23:51:19.905Z\",END-ON-NEXT=YES,X-TV-TWITCH-TRIGGER-URL=\"https://video-weaver.bar.hls.ttvnw.net/trigger/abc-123DEF_456\"" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:31:48.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/abc-123DEF_456.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:31:50.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/ghi-789JKL_012.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:31:52.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/mno-345PQR_678.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:31:54.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/stu-901VWX_234.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:31:56.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/yza-567BCD_890.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:31:58.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/efg-123hij_456.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:00.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/klm-789nop_012.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:02.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/qrs-345TUV_678.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:04.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/wxy-901ZAB_234.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:06.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/cde-567FGH_890.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:08.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/ijk-123lmn_456.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:10.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/opq-789RST_012.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:12.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/uvx-345YZA_678.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:14.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/bcd-901EFG_234.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-09-17T02:32:16.242Z\n#EXTINF:2.000,live\nhttps://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/hij-567KLM_890.ts" +
                "\n#EXT-X-TWITCH-PREFETCH:https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/nop-123QRS_456.ts" +
                "\n#EXT-X-TWITCH-PREFETCH:https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/tuv-789WXY_012.ts";

            var streamValues = new (DateTimeOffset programDateTime, decimal duration, bool isLive, string path)[]
            {
                (DateTimeOffset.Parse("2023-09-17T02:31:48.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/abc-123DEF_456.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:31:50.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/ghi-789JKL_012.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:31:52.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/mno-345PQR_678.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:31:54.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/stu-901VWX_234.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:31:56.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/yza-567BCD_890.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:31:58.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/efg-123hij_456.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:00.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/klm-789nop_012.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:02.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/qrs-345TUV_678.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:04.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/wxy-901ZAB_234.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:06.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/cde-567FGH_890.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:08.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/ijk-123lmn_456.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:10.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/opq-789RST_012.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:12.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/uvx-345YZA_678.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:14.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/bcd-901EFG_234.ts"),
                (DateTimeOffset.Parse("2023-09-17T02:32:16.242Z"), 2.000m, true, "https://video-edge-foo.bar.abs.hls.ttvnw.net/v1/segment/hij-567KLM_890.ts")
            };

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            M3U8 m3u8;
            if (useStream)
            {
                var bytes = Encoding.Unicode.GetBytes(EXAMPLE_M3U8_TWITCH);
                using var ms = new MemoryStream(bytes);
                m3u8 = M3U8.Parse(ms, Encoding.Unicode);
            }
            else
            {
                m3u8 = M3U8.Parse(EXAMPLE_M3U8_TWITCH);
            }

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(3u, m3u8.FileMetadata.Version);
            Assert.Equal(5u, m3u8.FileMetadata.StreamTargetDuration);
            Assert.Equal(4815u, m3u8.FileMetadata.MediaSequence);
            Assert.Equal(4997u, m3u8.FileMetadata.TwitchLiveSequence);
            Assert.Equal(9994.338m, m3u8.FileMetadata.TwitchElapsedSeconds);
            Assert.Equal(10028.338m, m3u8.FileMetadata.TwitchTotalSeconds);

            Assert.Equal(streamValues.Length, m3u8.Streams.Length);
            for (var i = 0; i < m3u8.Streams.Length; i++)
            {
                var stream = m3u8.Streams[i];
                var expectedStream = streamValues[i];
                Assert.Equal(expectedStream.programDateTime, stream.ProgramDateTime);
                Assert.Equal(expectedStream.duration, stream.PartInfo.Duration);
                Assert.Equal(expectedStream.isLive, stream.PartInfo.Live);
                Assert.Equal(expectedStream.path, stream.Path);
            }
        }

        [Theory]
        [InlineData(false, "en-US")]
        [InlineData(true, "en-US")]
        [InlineData(false, "ru-RU")]
        [InlineData(true, "ru-RU")]
        public void CorrectlyParsesTwitchM3U8OfPlaylists(bool useStream, string culture)
        {
            const string EXAMPLE_M3U8_TWITCH =
                "#EXTM3U" +
                "\n#EXT-X-TWITCH-INFO:ORIGIN=\"s3\",B=\"false\",REGION=\"NA\",USER-IP=\"255.255.255.255\",SERVING-ID=\"123abc456def789ghi012jkl345mno67\",CLUSTER=\"cloudfront_vod\",USER-COUNTRY=\"US\",MANIFEST-CLUSTER=\"cloudfront_vod\"" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"chunked\",NAME=\"1080p60\",AUTOSELECT=NO,DEFAULT=NO" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=5898203,CODECS=\"avc1.64002A,mp4a.40.2\",RESOLUTION=1920x1080,VIDEO=\"chunked\",FRAME-RATE=59.995" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/chunked/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"720p60\",NAME=\"720p60\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=3443956,CODECS=\"avc1.4D0020,mp4a.40.2\",RESOLUTION=1280x720,VIDEO=\"720p60\",FRAME-RATE=59.995" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/720p60/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"480p30\",NAME=\"480p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=1454397,CODECS=\"avc1.4D001F,mp4a.40.2\",RESOLUTION=852x480,VIDEO=\"480p30\",FRAME-RATE=29.998" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/480p30/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"audio_only\",NAME=\"Audio Only\",AUTOSELECT=NO,DEFAULT=NO" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=220328,CODECS=\"mp4a.40.2\",VIDEO=\"audio_only\"" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/audio_only/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"360p30\",NAME=\"360p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=708016,CODECS=\"avc1.4D001E,mp4a.40.2\",RESOLUTION=640x360,VIDEO=\"360p30\",FRAME-RATE=29.998" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/360p30/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"160p30\",NAME=\"160p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=284x160,VIDEO=\"160p30\",FRAME-RATE=29.998" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/160p30/index-dvr.m3u8";

            var streams = new M3U8.Stream[]
            {
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "chunked", "1080p60", false, false),
                    new M3U8.Stream.ExtStreamInfo(0, 5898203, new[] { "avc1.64002A", "mp4a.40.2" }, (1920, 1080), "chunked", 59.995m),
                    "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/chunked/index-dvr.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "720p60", "720p60", true, true),
                    new M3U8.Stream.ExtStreamInfo(0, 3443956, new[] { "avc1.4D0020", "mp4a.40.2" }, (1280, 720), "720p60", 59.995m),
                    "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/720p60/index-dvr.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "480p30", "480p", true, true),
                    new M3U8.Stream.ExtStreamInfo(0, 1454397, new[] { "avc1.4D001F", "mp4a.40.2" }, (852, 480), "480p30", 29.998m),
                    "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/480p30/index-dvr.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "audio_only", "Audio Only", false, false),
                    new M3U8.Stream.ExtStreamInfo(0, 220328, new[] { "mp4a.40.2" }, (0, 0), "audio_only", 0m),
                    "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/audio_only/index-dvr.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "360p30", "360p", true, true),
                    new M3U8.Stream.ExtStreamInfo(0, 708016, new[] { "avc1.4D001E", "mp4a.40.2" }, (640, 360), "360p30", 29.998m),
                    "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/360p30/index-dvr.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "160p30", "160p", true, true),
                    new M3U8.Stream.ExtStreamInfo(0, 288409, new[] { "avc1.4D000C", "mp4a.40.2" }, (284, 160), "160p30", 29.998m),
                    "https://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/160p30/index-dvr.m3u8")
            };

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            M3U8 m3u8;
            if (useStream)
            {
                var bytes = Encoding.Unicode.GetBytes(EXAMPLE_M3U8_TWITCH);
                using var ms = new MemoryStream(bytes);
                m3u8 = M3U8.Parse(ms, Encoding.Unicode);
            }
            else
            {
                m3u8 = M3U8.Parse(EXAMPLE_M3U8_TWITCH);
            }

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(streams.Length, m3u8.Streams.Length);
            Assert.Equivalent(streams[0], m3u8.Streams[0], true);
            Assert.Equivalent(streams[1], m3u8.Streams[1], true);
            Assert.Equivalent(streams[2], m3u8.Streams[2], true);
            Assert.Equivalent(streams[3], m3u8.Streams[3], true);
            Assert.Equivalent(streams[4], m3u8.Streams[4], true);
            Assert.Equivalent(streams[5], m3u8.Streams[5], true);
        }

        [Theory]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"chunked\",NAME=\"1080p60\",AUTOSELECT=NO,DEFAULT=NO", M3U8.Stream.ExtMediaInfo.MediaType.Video, "chunked", "1080p60", false, false)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"720p60\",NAME=\"720p60\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "720p60", "720p60", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"480p30\",NAME=\"480p\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "480p30", "480p", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"audio_only\",NAME=\"Audio Only\",AUTOSELECT=NO,DEFAULT=NO", M3U8.Stream.ExtMediaInfo.MediaType.Video, "audio_only", "Audio Only", false, false)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"360p30\",NAME=\"360p\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "360p30", "360p", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"160p30\",NAME=\"160p\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "160p30", "160p", true, true)]
        public void CorrectlyParsesTwitchM3U8MediaInfo(string mediaInfoString, M3U8.Stream.ExtMediaInfo.MediaType type, string groupId, string name, bool autoSelect, bool @default)
        {
            var mediaInfo = M3U8.Stream.ExtMediaInfo.Parse(mediaInfoString);

            Assert.Equal(type, mediaInfo.Type);
            Assert.Equal(groupId, mediaInfo.GroupId);
            Assert.Equal(name, mediaInfo.Name);
            Assert.Equal(autoSelect, mediaInfo.AutoSelect);
            Assert.Equal(@default, mediaInfo.Default);
        }

        [Theory]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=5898203,CODECS=\"avc1.64002A,mp4a.40.2\",RESOLUTION=1920x1080,VIDEO=\"chunked\",FRAME-RATE=59.995", 5898203, new[] { "avc1.64002A", "mp4a.40.2" }, 1920, 1080, "chunked", 59.995)]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=3443956,CODECS=\"avc1.4D0020,mp4a.40.2\",RESOLUTION=1280x720,VIDEO=\"720p60\",FRAME-RATE=59.995", 3443956, new[] { "avc1.4D0020", "mp4a.40.2" }, 1280, 720, "720p60", 59.995)]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=1454397,CODECS=\"avc1.4D001F,mp4a.40.2\",RESOLUTION=852x480,VIDEO=\"480p30\",FRAME-RATE=29.998", 1454397, new[] { "avc1.4D001F", "mp4a.40.2" }, 852, 480, "480p30", 29.998)]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=220328,CODECS=\"mp4a.40.2\",VIDEO=\"audio_only\"", 220328, new[] { "mp4a.40.2" }, 0, 0, "audio_only", 0)]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=708016,CODECS=\"avc1.4D001E,mp4a.40.2\",RESOLUTION=640x360,VIDEO=\"360p30\",FRAME-RATE=29.998", 708016, new[] { "avc1.4D001E", "mp4a.40.2" }, 640, 360, "360p30", 29.998)]
        [InlineData("#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=284x160,VIDEO=\"160p30\",FRAME-RATE=29.998", 288409, new[] { "avc1.4D000C", "mp4a.40.2" }, 284, 160, "160p30", 29.998)]
        public void CorrectlyParsesTwitchM3U8StreamInfo(string streamInfoString, int bandwidth, string[] codecs, uint videoWidth, uint videoHeight, string video, decimal framerate)
        {
            var streamInfo = M3U8.Stream.ExtStreamInfo.Parse(streamInfoString);

            Assert.Equal(bandwidth, streamInfo.Bandwidth);
            Assert.Equal(codecs, streamInfo.Codecs);
            Assert.Equal((videoWidth, videoHeight), streamInfo.Resolution);
            Assert.Equal(video, streamInfo.Video);
            Assert.Equal(framerate, streamInfo.Framerate);
        }

        [Theory]
        [InlineData(false, "en-US")]
        [InlineData(true, "en-US")]
        [InlineData(false, "ru-RU")]
        [InlineData(true, "ru-RU")]
        public void CorrectlyParsesKickM3U8OfTransportStreams(bool useStream, string culture)
        {
            const string EXAMPLE_M3U8_KICK =
                "#EXTM3U" +
                "\n#EXT-X-VERSION:4" +
                "\n#EXT-X-MEDIA-SEQUENCE:0" +
                "\n#EXT-X-TARGETDURATION:2" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:07.97Z\n#EXT-X-BYTERANGE:1601196@6470396\n#EXTINF:2.000,\n500.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:09.97Z\n#EXT-X-BYTERANGE:1588224@0\n#EXTINF:2.000,\n501.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:11.97Z\n#EXT-X-BYTERANGE:1579200@1588224\n#EXTINF:2.000,\n501.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:13.97Z\n#EXT-X-BYTERANGE:1646128@3167424\n#EXTINF:2.000,\n501.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:15.97Z\n#EXT-X-BYTERANGE:1587472@4813552\n#EXTINF:2.000,\n501.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:17.97Z\n#EXT-X-BYTERANGE:1594052@6401024\n#EXTINF:2.000,\n501.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:19.97Z\n#EXT-X-BYTERANGE:1851236@0\n#EXTINF:2.000,\n502.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:21.97Z\n#EXT-X-BYTERANGE:1437448@1851236\n#EXTINF:2.000,\n502.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:23.97Z\n#EXT-X-BYTERANGE:1535960@3288684\n#EXTINF:2.000,\n502.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:25.97Z\n#EXT-X-BYTERANGE:1568672@4824644\n#EXTINF:2.000,\n502.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:27.97Z\n#EXT-X-BYTERANGE:1625824@6393316\n#EXTINF:2.000,\n502.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:29.97Z\n#EXT-X-BYTERANGE:1583524@0\n#EXTINF:2.000,\n503.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:31.97Z\n#EXT-X-BYTERANGE:1597060@1583524\n#EXTINF:2.000,\n503.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:33.97Z\n#EXT-X-BYTERANGE:1642368@3180584\n#EXTINF:2.000,\n503.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:35.97Z\n#EXT-X-BYTERANGE:1556076@4822952\n#EXTINF:2.000,\n503.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:37.97Z\n#EXT-X-BYTERANGE:1669252@6379028\n#EXTINF:2.000,\n503.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:39.97Z\n#EXT-X-BYTERANGE:1544984@0\n#EXTINF:2.000,\n504.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:41.97Z\n#EXT-X-BYTERANGE:1601384@1544984\n#EXTINF:2.000,\n504.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:43.97Z\n#EXT-X-BYTERANGE:1672260@3146368\n#EXTINF:2.000,\n504.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:45.97Z\n#EXT-X-BYTERANGE:1623192@4818628\n#EXTINF:2.000,\n504.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:47.97Z\n#EXT-X-BYTERANGE:1526748@6441820\n#EXTINF:2.000,\n504.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:49.97Z\n#EXT-X-BYTERANGE:1731668@0\n#EXTINF:2.000,\n505.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:51.97Z\n#EXT-X-BYTERANGE:1454368@1731668\n#EXTINF:2.000,\n505.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:53.97Z\n#EXT-X-BYTERANGE:1572432@3186036\n#EXTINF:2.000,\n505.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:55.97Z\n#EXT-X-BYTERANGE:1625824@4758468\n#EXTINF:2.000,\n505.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:57.97Z\n#EXT-X-BYTERANGE:1616988@6384292\n#EXTINF:2.000,\n505.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:59.97Z\n#EXT-X-BYTERANGE:1632028@0\n#EXTINF:2.000,\n506.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:01.97Z\n#EXT-X-BYTERANGE:1543668@1632028\n#EXTINF:2.000,\n506.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:03.97Z\n#EXT-X-BYTERANGE:1768140@3175696\n#EXTINF:2.000,\n506.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:05.97Z\n#EXT-X-BYTERANGE:1519040@4943836\n#EXTINF:2.000,\n506.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:07.97Z\n#EXT-X-BYTERANGE:1506068@6462876\n#EXTINF:2.000,\n506.ts\n#EXT-X-ENDLIST";

            var streamValues = new (DateTimeOffset programDateTime, M3U8.ByteRange byteRange, string path)[]
            {
                (DateTimeOffset.Parse("2023-11-16T05:34:07.97Z"), (1601196, 6470396), "500.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:09.97Z"), (1588224, 0), "501.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:11.97Z"), (1579200, 1588224), "501.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:13.97Z"), (1646128, 3167424), "501.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:15.97Z"), (1587472, 4813552), "501.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:17.97Z"), (1594052, 6401024), "501.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:19.97Z"), (1851236, 0), "502.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:21.97Z"), (1437448, 1851236), "502.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:23.97Z"), (1535960, 3288684), "502.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:25.97Z"), (1568672, 4824644), "502.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:27.97Z"), (1625824, 6393316), "502.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:29.97Z"), (1583524, 0), "503.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:31.97Z"), (1597060, 1583524), "503.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:33.97Z"), (1642368, 3180584), "503.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:35.97Z"), (1556076, 4822952), "503.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:37.97Z"), (1669252, 6379028), "503.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:39.97Z"), (1544984, 0), "504.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:41.97Z"), (1601384, 1544984), "504.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:43.97Z"), (1672260, 3146368), "504.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:45.97Z"), (1623192, 4818628), "504.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:47.97Z"), (1526748, 6441820), "504.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:49.97Z"), (1731668, 0), "505.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:51.97Z"), (1454368, 1731668), "505.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:53.97Z"), (1572432, 3186036), "505.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:55.97Z"), (1625824, 4758468), "505.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:57.97Z"), (1616988, 6384292), "505.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:34:59.97Z"), (1632028, 0), "506.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:35:01.97Z"), (1543668, 1632028), "506.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:35:03.97Z"), (1768140, 3175696), "506.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:35:05.97Z"), (1519040, 4943836), "506.ts"),
                (DateTimeOffset.Parse("2023-11-16T05:35:07.97Z"), (1506068, 6462876), "506.ts")
            };

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            M3U8 m3u8;
            if (useStream)
            {
                var bytes = Encoding.Unicode.GetBytes(EXAMPLE_M3U8_KICK);
                using var ms = new MemoryStream(bytes);
                m3u8 = M3U8.Parse(ms, Encoding.Unicode);
            }
            else
            {
                m3u8 = M3U8.Parse(EXAMPLE_M3U8_KICK);
            }

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(4u, m3u8.FileMetadata.Version);
            Assert.Equal(2u, m3u8.FileMetadata.StreamTargetDuration);
            Assert.Equal(0u, m3u8.FileMetadata.MediaSequence);

            Assert.Equal(streamValues.Length, m3u8.Streams.Length);
            for (var i = 0; i < m3u8.Streams.Length; i++)
            {
                var stream = m3u8.Streams[i];
                Assert.Equal(2, stream.PartInfo.Duration);
                Assert.False(stream.PartInfo.Live);
                Assert.Equal(streamValues[i].programDateTime, stream.ProgramDateTime);
                Assert.Equal(streamValues[i].byteRange, stream.ByteRange);
                Assert.Equal(streamValues[i].path, stream.Path);
            }
        }

        [Theory]
        [InlineData(false, "en-US")]
        [InlineData(true, "en-US")]
        [InlineData(false, "ru-RU")]
        [InlineData(true, "ru-RU")]
        public void CorrectlyParsesKickM3U8OfPlaylists(bool useStream, string culture)
        {
            const string EXAMPLE_M3U8_KICK =
                "#EXTM3U" +
                "\n#EXT-X-SESSION-DATA:DATA-ID=\"net.live-video.content.id\",VALUE=\"AbC123dEf456\"" +
                "\n#EXT-X-SESSION-DATA:DATA-ID=\"net.live-video.customer.id\",VALUE=\"123456789012\"" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"1080p60\",NAME=\"1080p60\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=9878400,CODECS=\"avc1.64002A,mp4a.40.2\",RESOLUTION=1920x1080,VIDEO=\"1080p60\"" +
                "\n1080p60/playlist.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"720p60\",NAME=\"720p60\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=3330599,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=1280x720,VIDEO=\"720p60\"" +
                "\n720p60/playlist.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"480p30\",NAME=\"480p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=1335600,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=852x480,VIDEO=\"480p30\"" +
                "\n480p30/playlist.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"360p30\",NAME=\"360p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=630000,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=640x360,VIDEO=\"360p30\"" +
                "\n360p30/playlist.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"160p30\",NAME=\"160p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=230000,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=284x160,VIDEO=\"160p30\"" +
                "\n160p30/playlist.m3u8";

            var streams = new M3U8.Stream[]
            {
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "1080p60", "1080p60", true, true),
                    new M3U8.Stream.ExtStreamInfo(1, 9878400, new[] { "avc1.64002A", "mp4a.40.2" }, (1920, 1080), "1080p60", 60m),
                    "1080p60/playlist.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "720p60", "720p60", true, true),
                    new M3U8.Stream.ExtStreamInfo(1, 3330599, new[] { "avc1.4D401F", "mp4a.40.2" }, (1280, 720), "720p60", 60m),
                    "720p60/playlist.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "480p30", "480p", true, true),
                    new M3U8.Stream.ExtStreamInfo(1, 1335600, new[] { "avc1.4D401F", "mp4a.40.2" }, (852, 480), "480p30", 30m),
                    "480p30/playlist.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "360p30", "360p", true, true),
                    new M3U8.Stream.ExtStreamInfo(1, 630000, new[] { "avc1.4D401F", "mp4a.40.2" }, (640, 360), "360p30", 30m),
                    "360p30/playlist.m3u8"),
                new(new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, "160p30", "160p", true, true),
                    new M3U8.Stream.ExtStreamInfo(1, 230000, new[] { "avc1.4D401F", "mp4a.40.2" }, (284, 160), "160p30", 30m),
                    "160p30/playlist.m3u8")
            };

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            M3U8 m3u8;
            if (useStream)
            {
                var bytes = Encoding.Unicode.GetBytes(EXAMPLE_M3U8_KICK);
                using var ms = new MemoryStream(bytes);
                m3u8 = M3U8.Parse(ms, Encoding.Unicode);
            }
            else
            {
                m3u8 = M3U8.Parse(EXAMPLE_M3U8_KICK);
            }

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(streams.Length, m3u8.Streams.Length);
            Assert.Equivalent(streams[0], m3u8.Streams[0], true);
            Assert.Equivalent(streams[1], m3u8.Streams[1], true);
            Assert.Equivalent(streams[2], m3u8.Streams[2], true);
            Assert.Equivalent(streams[3], m3u8.Streams[3], true);
            Assert.Equivalent(streams[4], m3u8.Streams[4], true);
        }

        [Theory]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"1080p60\",NAME=\"1080p60\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "1080p60", "1080p60", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"720p60\",NAME=\"720p60\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "720p60", "720p60", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"480p30\",NAME=\"480p\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "480p30", "480p", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"360p30\",NAME=\"360p\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "360p30", "360p", true, true)]
        [InlineData("#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"160p30\",NAME=\"160p\",AUTOSELECT=YES,DEFAULT=YES", M3U8.Stream.ExtMediaInfo.MediaType.Video, "160p30", "160p", true, true)]
        public void CorrectlyParsesKickM3U8MediaInfo(string mediaInfoString, M3U8.Stream.ExtMediaInfo.MediaType type, string groupId, string name, bool autoSelect, bool @default)
        {
            var mediaInfo = M3U8.Stream.ExtMediaInfo.Parse(mediaInfoString);

            Assert.Equal(type, mediaInfo.Type);
            Assert.Equal(groupId, mediaInfo.GroupId);
            Assert.Equal(name, mediaInfo.Name);
            Assert.Equal(autoSelect, mediaInfo.AutoSelect);
            Assert.Equal(@default, mediaInfo.Default);
        }

        [Theory]
        [InlineData("#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=9878400,CODECS=\"avc1.64002A,mp4a.40.2\",RESOLUTION=1920x1080,VIDEO=\"1080p60\"", 9878400, new[] { "avc1.64002A", "mp4a.40.2" }, 1920, 1080, "1080p60")]
        [InlineData("#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=3330599,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=1280x720,VIDEO=\"720p60\"", 3330599, new[] { "avc1.4D401F", "mp4a.40.2" }, 1280, 720, "720p60")]
        [InlineData("#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=1335600,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=852x480,VIDEO=\"480p30\"", 1335600, new[] { "avc1.4D401F", "mp4a.40.2" }, 852, 480, "480p30")]
        [InlineData("#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=630000,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=640x360,VIDEO=\"360p30\"", 630000, new[] { "avc1.4D401F", "mp4a.40.2" }, 640, 360, "360p30")]
        [InlineData("#EXT-X-STREAM-INF:PROGRAM-ID=1,BANDWIDTH=230000,CODECS=\"avc1.4D401F,mp4a.40.2\",RESOLUTION=284x160,VIDEO=\"160p30\"", 230000, new[] { "avc1.4D401F", "mp4a.40.2" }, 284, 160, "160p30")]
        public void CorrectlyParsesKickM3U8StreamInfo(string streamInfoString, int bandwidth, string[] codecs, uint videoWidth, uint videoHeight, string video)
        {
            var streamInfo = M3U8.Stream.ExtStreamInfo.Parse(streamInfoString);

            Assert.Equal(bandwidth, streamInfo.Bandwidth);
            Assert.Equal(codecs, streamInfo.Codecs);
            Assert.Equal((videoWidth, videoHeight), streamInfo.Resolution);
            Assert.Equal(video, streamInfo.Video);
        }

        [Theory]
        [InlineData(100, 200, "100@200", "")]
        [InlineData(100, 200, "#EXT-X-BYTERANGE:100@200", "#EXT-X-BYTERANGE:")]
        [InlineData(100, 200, "BYTERANGE=100@200", "BYTERANGE=")]
        public void CorrectlyParsesByteRange(uint length, uint start, string byteRangeString, string key)
        {
            var expected = new M3U8.ByteRange(length, start);

            var actual = M3U8.ByteRange.Parse(byteRangeString, key);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("429496729500@1")]
        [InlineData("1@429496729500")]
        [InlineData("42949672950000")]
        public void ThrowsFormatExceptionForBadByteRangeString(string byteRangeString)
        {
            Assert.Throws<FormatException>(() => M3U8.ByteRange.Parse(byteRangeString, default));
        }

        [Theory]
        [InlineData(100, 200, "100x200")]
        [InlineData(100, 200, "RESOLUTION=100x200")]
        public void CorrectlyParsesResolution(uint width, uint height, string byteRangeString)
        {
            var expected = new M3U8.Stream.ExtStreamInfo.StreamResolution(width, height);

            var actual = M3U8.Stream.ExtStreamInfo.StreamResolution.Parse(byteRangeString);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("429496729500x1")]
        [InlineData("1x429496729500")]
        [InlineData("42949672950000")]
        public void ThrowsFormatExceptionForBadResolutionString(string resolutionString)
        {
            Assert.Throws<FormatException>(() => M3U8.Stream.ExtStreamInfo.StreamResolution.Parse(resolutionString));
        }

        [Theory]
        [InlineData("en-GB")]
        [InlineData("tr-TR")]
        [InlineData("ru-RU")]
        public void CorrectlyStringifiesInvariantOfCulture(string culture)
        {
            const string EXAMPLE_M3U8 =
                "#EXTM3U" +
                "\n#EXT-X-TWITCH-INFO:ORIGIN=\"s3\",B=\"false\",REGION=\"NA\",USER-IP=\"255.255.255.255\",SERVING-ID=\"123abc456def789ghi012jkl345mno67\",CLUSTER=\"cloudfront_vod\",USER-COUNTRY=\"US\",MANIFEST-CLUSTER=\"cloudfront_vod\"" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"chunked\",NAME=\"1080p60\",AUTOSELECT=NO,DEFAULT=NO" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=5898203,CODECS=\"avc1.64002A,mp4a.40.2\",RESOLUTION=1920x1080,VIDEO=\"chunked\",FRAME-RATE=59.995" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/chunked/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"720p60\",NAME=\"720p60\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=3443956,CODECS=\"avc1.4D0020,mp4a.40.2\",RESOLUTION=1280x720,VIDEO=\"720p60\",FRAME-RATE=59.995" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/720p60/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"480p30\",NAME=\"480p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=1454397,CODECS=\"avc1.4D001F,mp4a.40.2\",RESOLUTION=852x480,VIDEO=\"480p30\",FRAME-RATE=29.998" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/480p30/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"audio_only\",NAME=\"Audio Only\",AUTOSELECT=NO,DEFAULT=NO" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=220328,CODECS=\"mp4a.40.2\",VIDEO=\"audio_only\"" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/audio_only/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"360p30\",NAME=\"360p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=708016,CODECS=\"avc1.4D001E,mp4a.40.2\",RESOLUTION=640x360,VIDEO=\"360p30\",FRAME-RATE=29.998" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/360p30/index-dvr.m3u8" +
                "\n#EXT-X-MEDIA:TYPE=VIDEO,GROUP-ID=\"160p30\",NAME=\"160p\",AUTOSELECT=YES,DEFAULT=YES" +
                "\n#EXT-X-STREAM-INF:BANDWIDTH=288409,CODECS=\"avc1.4D000C,mp4a.40.2\",RESOLUTION=284x160,VIDEO=\"160p30\",FRAME-RATE=29.998" +
                "\nhttps://abc123def456gh.cloudfront.net/123abc456def789ghi01_streamer42_12345678901_1234567890/160p30/index-dvr.m3u8" +
                "\n#EXT-X-VERSION:4" +
                "\n#EXT-X-MEDIA-SEQUENCE:0" +
                "\n#EXT-X-TARGETDURATION:2" +
                "\n#EXT-X-MAP:URI=\"init-0.mp4\"" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:07.97Z\n#EXT-X-BYTERANGE:1601196@6470396\n#EXTINF:2.000,\n500.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:09.97Z\n#EXT-X-BYTERANGE:1588224@0\n#EXTINF:2.000,\n501.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:11.97Z\n#EXT-X-BYTERANGE:1579200@1588224\n#EXTINF:2.000,\n501.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:13.97Z\n#EXT-X-BYTERANGE:1646128@3167424\n#EXTINF:2.000,\n501.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:15.97Z\n#EXT-X-BYTERANGE:1587472@4813552\n#EXTINF:2.000,\n501.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:17.97Z\n#EXT-X-BYTERANGE:1594052@6401024\n#EXTINF:2.000,\n501.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:19.97Z\n#EXT-X-BYTERANGE:1851236@0\n#EXTINF:2.000,\n502.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:21.97Z\n#EXT-X-BYTERANGE:1437448@1851236\n#EXTINF:2.000,\n502.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:23.97Z\n#EXT-X-BYTERANGE:1535960@3288684\n#EXTINF:2.000,\n502.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:25.97Z\n#EXT-X-BYTERANGE:1568672@4824644\n#EXTINF:2.000,\n502.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:27.97Z\n#EXT-X-BYTERANGE:1625824@6393316\n#EXTINF:2.000,\n502.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:29.97Z\n#EXT-X-BYTERANGE:1583524@0\n#EXTINF:2.000,\n503.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:31.97Z\n#EXT-X-BYTERANGE:1597060@1583524\n#EXTINF:2.000,\n503.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:33.97Z\n#EXT-X-BYTERANGE:1642368@3180584\n#EXTINF:2.000,\n503.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:35.97Z\n#EXT-X-BYTERANGE:1556076@4822952\n#EXTINF:2.000,\n503.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:37.97Z\n#EXT-X-BYTERANGE:1669252@6379028\n#EXTINF:2.000,\n503.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:39.97Z\n#EXT-X-BYTERANGE:1544984@0\n#EXTINF:2.000,\n504.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:41.97Z\n#EXT-X-BYTERANGE:1601384@1544984\n#EXTINF:2.000,\n504.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:43.97Z\n#EXT-X-BYTERANGE:1672260@3146368\n#EXTINF:2.000,\n504.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:45.97Z\n#EXT-X-BYTERANGE:1623192@4818628\n#EXTINF:2.000,\n504.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:47.97Z\n#EXT-X-BYTERANGE:1526748@6441820\n#EXTINF:2.000,\n504.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:49.97Z\n#EXT-X-BYTERANGE:1731668@0\n#EXTINF:2.000,\n505.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:51.97Z\n#EXT-X-BYTERANGE:1454368@1731668\n#EXTINF:2.000,\n505.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:53.97Z\n#EXT-X-BYTERANGE:1572432@3186036\n#EXTINF:2.000,\n505.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:55.97Z\n#EXT-X-BYTERANGE:1625824@4758468\n#EXTINF:2.000,\n505.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:57.97Z\n#EXT-X-BYTERANGE:1616988@6384292\n#EXTINF:2.000,\n505.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:34:59.97Z\n#EXT-X-BYTERANGE:1632028@0\n#EXTINF:2.000,\n506.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:01.97Z\n#EXT-X-BYTERANGE:1543668@1632028\n#EXTINF:2.000,\n506.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:03.97Z\n#EXT-X-BYTERANGE:1768140@3175696\n#EXTINF:2.000,\n506.ts\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:05.97Z\n#EXT-X-BYTERANGE:1519040@4943836\n#EXTINF:2.000,\n506.ts" +
                "\n#EXT-X-PROGRAM-DATE-TIME:2023-11-16T05:35:07.97Z\n#EXT-X-BYTERANGE:1506068@6462876\n#EXTINF:2.000,\n506.ts\n#EXT-X-ENDLIST";

            var m3u8 = M3U8.Parse(EXAMPLE_M3U8);

            var oldCulture = CultureInfo.CurrentCulture;

            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var stringExpected = m3u8.ToString();
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            var stringActual = m3u8.ToString();

            CultureInfo.CurrentCulture = oldCulture;

            Assert.Equal(stringExpected, stringActual);
        }
    }
}