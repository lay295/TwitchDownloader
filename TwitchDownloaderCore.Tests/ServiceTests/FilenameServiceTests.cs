using TwitchDownloaderCore.Services;

namespace TwitchDownloaderCore.Tests.ServiceTests
{
    // Tests FilenameService: the {title}/{date}/{trim_start}... file name templates of the GUI and CLI, invalid-character replacement and
    // the '(n)' collision renaming. A wrong template expansion or an unescaped char breaks every download's output path.
    public class FilenameServiceTests
    {
        private static (string title, string id, DateTime date, string channel, string channelId, TimeSpan trimStart, TimeSpan trimEnd, TimeSpan videoLength, int viewCount, string game, string clipper, string clipperId) GetExampleInfo() =>
            ("A Title", "abc123", new DateTime(1984, 11, 1, 9, 43, 21), "streamer8", "123456789", new TimeSpan(0, 1, 2, 3, 4), new TimeSpan(0, 5, 6, 7, 8), new TimeSpan(0, 9, 10, 11, 12), 123456789, "A Game", "viewer8", "987654321");

        // Every supported template variable, including the custom date/time formats, expands to the documented text.
        [Theory]
        [InlineData("{title}", "A Title")]
        [InlineData("{id}", "abc123")]
        [InlineData("{channel}", "streamer8")]
        [InlineData("{channel_id}", "123456789")]
        [InlineData("{date}", "11-1-84")]
        [InlineData("{trim_start}", "01-02-03")]
        [InlineData("{trim_end}", "05-06-07")]
        [InlineData("{trim_length}", "04-04-04")]
        [InlineData("{length}", "09-10-11")]
        [InlineData("{views}", "123456789")]
        [InlineData("{game}", "A Game")]
        [InlineData("{clipper}", "viewer8")]
        [InlineData("{clipper_id}", "987654321")]
        [InlineData("{date_custom=\"s\"}", "1984-11-01T09_43_21")]
        [InlineData("{trim_start_custom=\"hh\\-mm\\-ss\"}", "01-02-03")]
        [InlineData("{trim_end_custom=\"hh\\-mm\\-ss\"}", "05-06-07")]
        [InlineData("{trim_length_custom=\"hh\\-mm\\-ss\\-fff\"}", "04-04-04-004")]
        [InlineData("{length_custom=\"hh\\-mm\\-ss\\-fff\"}", "09-10-11-012")]
        public void CorrectlyGeneratesIndividualTemplates(string template, string expected)
        {
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(template, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(expected, result);
        }

        // Realistic templates mixing many variables and literal text expand correctly in one pass.
        [Theory]
        [InlineData("[{date_custom=\"M-dd-yy\"}] {channel} - {title}", "[11-01-84] streamer8 - A Title")]
        [InlineData("[{channel}] [{date_custom=\"M-dd-yy\"}] [{game}] {title} ({id}) - {views} views", "[streamer8] [11-01-84] [A Game] A Title (abc123) - 123456789 views")]
        [InlineData("{title} by {channel} playing {game} on {date_custom=\"M dd, yyyy\"} for {length_custom=\"h'h 'm'm 's's'\"} trimmed to {trim_length_custom=\"h'h 'm'm 's's'\"} with {views} views", "A Title by streamer8 playing A Game on 11 01, 1984 for 9h 10m 11s trimmed to 4h 4m 4s with 123456789 views")]
        public void CorrectlyGeneratesLargeTemplates(string template, string expected)
        {
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(template, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(expected, result);
        }

        // Several custom-format variables (and repeats of the same one) are each expanded with their own format string.
        [Fact]
        public void CorrectlyInterpretsMultipleCustomParameters()
        {
            const string TEMPLATE = "{date_custom=\"yyyy\"} {date_custom=\"MM\"} {date_custom=\"dd\"} {trim_start_custom=\"hh\\-mm\\-ss\"} {trim_end_custom=\"hh\\-mm\\-ss\"} {trim_length_custom=\"hh\\-mm\\-ss\"} {length_custom=\"hh\\-mm\\-ss\"}";
            const string EXPECTED = "1984 11 01 01-02-03 05-06-07 04-04-04 09-10-11";
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(EXPECTED, result);
        }

        // Forward slashes in a template create sub folders (platform separator in the result) rather than being escaped.
        [Fact]
        public void CorrectlyGeneratesSubFolders_WithForwardSlash()
        {
            const string TEMPLATE = "{channel}/{date_custom=\"yyyy\"}/{date_custom=\"MM\"}/{date_custom=\"dd\"}/{title}.mp4";
            var expected = Path.Combine("streamer8", "1984", "11", "01", "A Title.mp4");
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(expected, result);
        }

        // Backslashes in a template create sub folders too, so templates written on Windows work everywhere.
        [Fact]
        public void CorrectlyGeneratesSubFolders_WithBackSlash()
        {
            const string TEMPLATE = "{channel}\\{date_custom=\"yyyy\"}\\{date_custom=\"MM\"}\\{date_custom=\"dd\"}\\{title}";
            var expected = Path.Combine("streamer8", "1984", "11", "01", "A Title");
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(expected, result);
        }

        // Metadata values (title, channel, game...) containing Windows-invalid chars are mapped to their full-width look-alikes so the name
        // stays readable but valid; a slash in a title must not create a folder.
        [Theory]
        [InlineData("{title}")]
        [InlineData("{id}")]
        [InlineData("{channel}")]
        [InlineData("{channel_id}")]
        [InlineData("{clipper}")]
        [InlineData("{clipper_id}")]
        [InlineData("{game}")]
        public void CorrectlyReplacesInvalidCharactersForNonCustomTemplates(string template)
        {
            const string INVALID_CHARS = "\"*:<>?|/\\";
            const string EXPECTED = "＂＊：＜＞？｜／＼";

            var result = FilenameService.GetFilename(template, INVALID_CHARS, INVALID_CHARS, default, INVALID_CHARS, INVALID_CHARS, default, default, default, default, INVALID_CHARS, INVALID_CHARS, INVALID_CHARS);

            Assert.Equal(EXPECTED, result);
        }

        // Literal text inside custom date/time format strings is sanitised the same way.
        [Theory]
        [InlineData("{date_custom=\"'")]
        [InlineData("{trim_start_custom=\"'")]
        [InlineData("{trim_end_custom=\"'")]
        [InlineData("{length_custom=\"'")]
        public void CorrectlyReplacesInvalidCharactersForCustomTemplates(string templateStart)
        {
            const string EXPECTED = "＂＊：＜＞？｜／＼";
            const string INVALID_CHARS = "\"*:<>?|/\\\\";
            var template = templateStart + INVALID_CHARS + "'\"}";

            var result = FilenameService.GetFilename(template, INVALID_CHARS, INVALID_CHARS, default, INVALID_CHARS, INVALID_CHARS, default, default, default, default, INVALID_CHARS, INVALID_CHARS, INVALID_CHARS);

            Assert.Equal(EXPECTED, result);
        }

        // Invalid chars in a sub folder segment are sanitised while the folder split itself is kept.
        [Fact]
        public void CorrectlyReplacesInvalidCharactersForSubFolders()
        {
            const string INVALID_CHARS = "\"*:<>?|";
            const string FULL_WIDTH_CHARS = "＂＊：＜＞？｜";
            const string TEMPLATE = INVALID_CHARS + "\\{title}";
            var expected = Path.Combine(FULL_WIDTH_CHARS, "A Title");
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(expected, result);
        }

        // {random_string} must differ between two expansions, otherwise it could not serve as a collision avoider.
        [Fact]
        public void RandomStringIsRandom()
        {
            const string TEMPLATE = "{random_string}";
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);
            var result2 = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.NotEqual(result, result2);
        }

        // Unknown {placeholders} are left as-is instead of being removed or throwing.
        [Fact]
        public void DoesNotInterpretBogusTemplateParameter()
        {
            const string TEMPLATE = "{foobar}";
            const string EXPECTED = "{foobar}";
            var (title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, channelId, trimStart, trimEnd, videoLength, viewCount, game, clipper, clipperId);

            Assert.Equal(EXPECTED, result);
        }

        // Null strings and default times/dates (e.g. a clip without a game) expand to empty/zero text rather than throwing.
        [Fact]
        public void GetFilenameDoesNotThrow_WhenNullOrDefaultInput()
        {
            const string TEMPLATE = "{title}_{id}_{date}_{channel}_{channel_id}_{trim_start}_{trim_end}_{length}_{views}_{game}_{clipper}_{clipper_id}_{date_custom=\"s\"}_{trim_start_custom=\"hh\\-mm\\-ss\"}_{trim_end_custom=\"hh\\-mm\\-ss\"}_{length_custom=\"hh\\-mm\\-ss\"}";
            const string EXPECTED = "__1-1-01___00-00-00_00-00-00_00-00-00_0____0001-01-01T00_00_00_00-00-00_00-00-00_00-00-00";

            var result = FilenameService.GetFilename(TEMPLATE, default, default, default, default, default, default, default, default, default, default, default);

            Assert.Equal(EXPECTED, result);
        }

        // Each Windows-invalid char is mapped to its full-width form, and control chars (no look-alike) become '_'.
        [Theory]
        [InlineData("\"", "＂")]
        [InlineData("*", "＊")]
        [InlineData(":", "：")]
        [InlineData("<", "＜")]
        [InlineData(">", "＞")]
        [InlineData("?", "？")]
        [InlineData("|", "｜")]
        [InlineData("/", "／")]
        [InlineData("\\", "＼")]
        [InlineData("\0", "_")]
        public void CorrectlyReplacesInvalidFilenameCharacters(string str, string expected)
        {
            var actual = FilenameService.ReplaceInvalidFilenameChars(str);

            Assert.Equal(expected, actual);
        }

        // A null file name passes through as null (annotated NotNullIfNotNull).
        [Fact]
        public void ReplaceInvalidFilenameCharactersDoesNotThrow_WhenNullInput()
        {
            const string? STR = null;
            const string? EXPECTED = null;

            var actual = FilenameService.ReplaceInvalidFilenameChars(STR);

            Assert.Equal(EXPECTED, actual);
        }

        // When the file does not exist its name is returned unchanged.
        [Fact]
        public void GetNonCollidingNameWorks_WhenNoCollisionExists()
        {
            var expected = Path.Combine(Path.GetTempPath(), "foo.txt");
            var path = Path.Combine(Path.GetTempPath(), "foo.txt");
            var fileInfo = new FileInfo(path);

            try
            {
                var actual = FilenameService.GetNonCollidingName(fileInfo);

                Assert.Equal(expected, actual.FullName);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // When the file exists the first free 'name (n).ext' is chosen, matching Windows Explorer's convention.
        [Fact]
        public void GetNonCollidingNameWorks_WhenCollisionExists()
        {
            var expected = Path.Combine(Path.GetTempPath(), "foo (1).txt");
            var path = Path.Combine(Path.GetTempPath(), "foo.txt");
            var fileInfo = new FileInfo(path);

            try
            {
                fileInfo.Create().Close();

                var actual = FilenameService.GetNonCollidingName(fileInfo);

                Assert.Equal(expected, actual.FullName);
            }
            finally
            {
                File.Delete(path);
            }
        }

        // GuessVodFileExtension picks the container for a VOD download from the requested quality: audio-only qualities become
        // .m4a so players open them as audio, everything else (including no quality = best) is .mp4.
        [Theory]
        [InlineData(null, ".mp4")]
        [InlineData("", ".mp4")]
        [InlineData("  ", ".mp4")]
        [InlineData("1080p60", ".mp4")]
        [InlineData("Audio Only", ".m4a")]
        [InlineData("audio", ".m4a")]
        public void GuessVodFileExtension_UsesM4aOnlyForAudio(string? qualityString, string expected)
        {
            Assert.Equal(expected, FilenameService.GuessVodFileExtension(qualityString));
        }
    }
}
