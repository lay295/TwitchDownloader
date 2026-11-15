using System.Globalization;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests FfmpegMetadata, which writes the FFMETADATA file that ffmpeg embeds into downloaded VODs and clips
    // (title, streamer, date, description, chapters). Escaping mistakes here corrupt or truncate the embedded metadata.
    public class FfmpegMetadataTests
    {
        private static readonly DateTime CreatedAt = new(2023, 11, 16, 5, 34, 7, DateTimeKind.Utc);

        private static VideoMomentEdge Chapter(string type, int position, int duration, string? game, string description = "") => new()
        {
            node = new VideoMoment
            {
                _type = type,
                positionMilliseconds = position,
                durationMilliseconds = duration,
                description = description,
                details = new GameChangeMomentDetails { game = game is null ? null : new Game { displayName = game } }
            }
        };

        private static VideoInfo Video(string? displayName = "Streamer", string? login = "streamer") => new()
        {
            title = @"Speedrun 'any%' \ PB",
            createdAt = CreatedAt,
            viewCount = 1234,
            owner = new VideoOwner { displayName = displayName, login = login },
            game = new Game { displayName = "Just Chatting" },
            description = "Line 1  \nLine 2\n\nLine 3\n\n"
        };

        // Runs the serializer under a fixed culture and returns the written file.
        private static async Task<string> Serialize(Func<string, Task> serialize, string culture = "en-US")
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("metadata.txt");
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                await serialize(path);
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
            }

            return await File.ReadAllTextAsync(path);
        }

        /// <summary>
        /// The VOD file contains escaped title/id, artist, year, genre, a de-duplicated multi-line description and stats in the
        /// comment, and one chapter per GAME_CHANGE moment (other moment types ignored; missing game falls back to the description).
        /// Chapters past the video's end are dropped and a chapter running past the end is clamped to it.
        /// </summary>
        [Fact]
        public async Task SerializesVodMetadataAndChapters()
        {
            var chapters = new[]
            {
                Chapter("GAME_CHANGE", 0, 60_000, "Game A"),
                Chapter("OTHER", 10_000, 1_000, "Ignored"),
                Chapter("GAME_CHANGE", 60_000, 120_000, null, "Game B"),
                Chapter("GAME_CHANGE", 500_000, 1_000, "Past the end"),
            };

            var text = await Serialize(path => FfmpegMetadata.SerializeAsync(path, "123", Video(), TimeSpan.Zero, TimeSpan.FromSeconds(150), chapters));

            Assert.Equal(
                ";FFMETADATA1\n" +
                "title=Speedrun \\'any%\\' \\\\ PB (123)\n" +
                "artist=Streamer\n" +
                "date=2023\n" +
                "genre=Just Chatting\n" +
                "comment=Line 1\\\nLine 2\\\nLine 3\\\n" +
                "------------------------\\\n" +
                "Created at: 2023-11-16 05:34:07Z\\\n" +
                "Video id: 123\\\n" +
                "Views: 1234\n" +
                "[CHAPTER]\nTIMEBASE=1/1000\nSTART=0\nEND=60000\ntitle=Game A\n" +
                "[CHAPTER]\nTIMEBASE=1/1000\nSTART=60000\nEND=150000\ntitle=Game B\n",
                text);
        }

        /// <summary>When the start of the VOD is trimmed, chapter times are shifted so they line up with the trimmed output.</summary>
        [Fact]
        public async Task ShiftsChaptersByTrimStart()
        {
            var text = await Serialize(path =>
                FfmpegMetadata.SerializeAsync(path, "123", Video(), TimeSpan.FromSeconds(30), TimeSpan.Zero, [Chapter("GAME_CHANGE", 60_000, 1_000, "Game")]));

            Assert.EndsWith("[CHAPTER]\nTIMEBASE=1/1000\nSTART=30000\nEND=31000\ntitle=Game\n", text);
        }

        /// <summary>
        /// The artist is the display name, with the login appended only for non-ASCII display names (so it stays searchable);
        /// it falls back to the login, and the line is omitted when neither is known.
        /// </summary>
        [Theory]
        [InlineData("Streamer", "streamer", "artist=Streamer\n")]
        [InlineData("ストリーマー", "streamer", "artist=ストリーマー (streamer)\n")]
        [InlineData(null, "streamer", "artist=streamer\n")]
        [InlineData("Streamer", null, "artist=Streamer\n")]
        [InlineData(null, " ", null)]
        public async Task ChoosesArtistName(string? displayName, string? login, string? expectedLine)
        {
            var text = await Serialize(path => FfmpegMetadata.SerializeAsync(path, "123", Video(displayName, login), TimeSpan.Zero, TimeSpan.Zero, null!));

            if (expectedLine is null)
                Assert.DoesNotContain("artist=", text);
            else
                Assert.Contains(expectedLine, text);
        }

        /// <summary>
        /// Clips record the clipper in the comment, use the clip slug as id, and write no chapters when none are given;
        /// a missing description must not produce an empty comment block.
        /// </summary>
        [Fact]
        public async Task SerializesClipMetadata()
        {
            var clip = new ShareClipRenderStatusClip
            {
                title = "Big play",
                createdAt = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc),
                viewCount = 99,
                broadcaster = new ShareClipRenderStatusBroadcaster { displayName = "Streamer", login = "streamer" },
                curator = new ShareClipRenderStatusCurator { displayName = "Clipper", login = "clipper" },
                game = new ShareClipRenderStatusGame { displayName = "Game" }
            };

            var text = await Serialize(path => FfmpegMetadata.SerializeAsync(path, "SlugId", clip, null!));

            Assert.Equal(
                ";FFMETADATA1\n" +
                "title=Big play (SlugId)\n" +
                "artist=Streamer\n" +
                "date=2024\n" +
                "genre=Game\n" +
                "comment=Clipped by: Clipper\\\n" +
                "Created at: 2024-02-03 04:05:06Z\\\n" +
                "Video id: SlugId\\\n" +
                "Views: 99\n",
                text);
        }

        /// <summary>
        /// The 'date' key must hold the Gregorian year regardless of the user's culture. It is formatted with the current culture,
        /// so on a Thai (Buddhist calendar) system 2023 is written as 2566.
        /// </summary>
        [Fact(Skip = "Bug: FfmpegMetadata formats 'date' with the current culture, so non-Gregorian calendars (e.g. th-TH) write the wrong year")]
        public async Task WritesGregorianYearRegardlessOfCulture()
        {
            var text = await Serialize(path => FfmpegMetadata.SerializeAsync(path, "123", Video(), TimeSpan.Zero, TimeSpan.Zero, null!), "th-TH");

            Assert.Contains("date=2023\n", text);
        }
    }
}
