using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests
{
    public class FilenameServiceTests
    {
        private static (string title, string id, DateTime date, string channel, TimeSpan cropStart, TimeSpan cropEnd, string viewCount, string game) GetExampleInfo() =>
            ("A Title", "abc123", new DateTime(1984, 11, 1, 9, 43, 21), "streamer8", new TimeSpan(0, 1, 2, 3, 4), new TimeSpan(0, 5, 6, 7, 8), "123456789", "A Game");

        [Theory]
        [InlineData("{title}", "A Title")]
        [InlineData("{id}", "abc123")]
        [InlineData("{channel}", "streamer8")]
        [InlineData("{date}", "11-1-84")]
        [InlineData("{crop_start}", "01-02-03")]
        [InlineData("{crop_end}", "05-06-07")]
        [InlineData("{length}", "04-04-04")]
        [InlineData("{views}", "123456789")]
        [InlineData("{game}", "A Game")]
        [InlineData("{date_custom=\"s\"}", "1984-11-01T09_43_21")]
        [InlineData("{crop_start_custom=\"hh\\-mm\\-ss\"}", "01-02-03")]
        [InlineData("{crop_end_custom=\"hh\\-mm\\-ss\"}", "05-06-07")]
        [InlineData("{length_custom=\"hh\\-mm\\-ss\"}", "04-04-04")]
        public void CorrectlyGeneratesIndividualTemplates(string template, string expected)
        {
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(template, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("[{date_custom=\"M-dd-yy\"}] {channel} - {title}", "[11-01-84] streamer8 - A Title")]
        [InlineData("[{channel}] [{date_custom=\"M-dd-yy\"}] [{game}] {title} ({id}) - {views} views", "[streamer8] [11-01-84] [A Game] A Title (abc123) - 123456789 views")]
        [InlineData("{title} by {channel} playing {game} on {date_custom=\"M dd, yyyy\"} for {length_custom=\"h'h 'm'm 's's'\"} with {views} views", "A Title by streamer8 playing A Game on 11 01, 1984 for 4h 4m 4s with 123456789 views")]
        public void CorrectlyGeneratesLargeTemplates(string template, string expected)
        {
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(template, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CorrectlyInterpretsMultipleCustomParameters()
        {
            const string TEMPLATE = "{date_custom=\"yyyy\"} {date_custom=\"MM\"} {date_custom=\"dd\"} {crop_start_custom=\"hh\\-mm\\-ss\"} {crop_end_custom=\"hh\\-mm\\-ss\"} {length_custom=\"hh\\-mm\\-ss\"}";
            const string EXPECTED = "1984 11 01 01-02-03 05-06-07 04-04-04";
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(EXPECTED, result);
        }

        [Fact]
        public void CorrectlyGeneratesSubFolders_WithForwardSlash()
        {
            const string TEMPLATE = "{channel}/{date_custom=\"yyyy\"}/{date_custom=\"MM\"}/{date_custom=\"dd\"}/{title}.mp4";
            var expected = Path.Combine("streamer8", "1984", "11", "01", "A Title.mp4");
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CorrectlyGeneratesSubFolders_WithBackSlash()
        {
            const string TEMPLATE = "{channel}\\{date_custom=\"yyyy\"}\\{date_custom=\"MM\"}\\{date_custom=\"dd\"}\\{title}";
            var expected = Path.Combine("streamer8", "1984", "11", "01", "A Title");
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{title}")]
        [InlineData("{id}")]
        [InlineData("{channel}")]
        [InlineData("{views}")]
        [InlineData("{game}")]
        public void CorrectlyReplacesInvalidCharactersForNonCustomTemplates(string template)
        {
            const char EXPECTED = '_';
            var invalidChars = new string(Path.GetInvalidFileNameChars());

            var result = FilenameService.GetFilename(template, invalidChars, invalidChars, default, invalidChars, default, default, invalidChars, invalidChars);

            Assert.All(result, c => Assert.Equal(EXPECTED, c));
        }

        [Theory]
        [InlineData("{date_custom=\"'")]
        [InlineData("{crop_start_custom=\"'")]
        [InlineData("{crop_end_custom=\"'")]
        [InlineData("{length_custom=\"'")]
        public void CorrectlyReplacesInvalidCharactersForCustomTemplates(string templateStart)
        {
            const char EXPECTED = '_';
            var invalidChars = new string(Path.GetInvalidFileNameChars());
            var template = string.Concat(
                templateStart,
                invalidChars.ReplaceAny("\r\n", EXPECTED), // newline chars are not supported by the custom parameters. This will not change.
                "'\"}");

            var result = FilenameService.GetFilename(template, invalidChars, invalidChars, default, invalidChars, default, default, invalidChars, invalidChars);

            Assert.All(result, c => Assert.Equal(EXPECTED, c));
        }

        [Fact]
        public void CorrectlyReplacesInvalidCharactersForSubFolders()
        {
            var invalidChars = new string(Path.GetInvalidPathChars());
            var template = invalidChars + "\\{title}";
            var expected = Path.Combine(new string('_', invalidChars.Length), "A Title");
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(template, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void RandomStringIsRandom()
        {
            const string TEMPLATE = "{random_string}";
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, cropStart, cropEnd, viewCount, game);
            var result2 = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.NotEqual(result, result2);
        }

        [Fact]
        public void DoesNotInterpretBogusTemplateParameter()
        {
            const string TEMPLATE = "{foobar}";
            const string EXPECTED = "{foobar}";
            var (title, id, date, channel, cropStart, cropEnd, viewCount, game) = GetExampleInfo();

            var result = FilenameService.GetFilename(TEMPLATE, title, id, date, channel, cropStart, cropEnd, viewCount, game);

            Assert.Equal(EXPECTED, result);
        }
    }
}