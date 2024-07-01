using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests;

public class FilenameServiceTests {
    private static (string title, string id, DateTime date, string channel, TimeSpan trimStart, TimeSpan trimEnd, int
        viewCount, string game) GetExampleInfo() =>
        ("A Title", "abc123", new(1984, 11, 1, 9, 43, 21), "streamer8", new(0, 1, 2, 3, 4), new(0, 5, 6, 7, 8),
            123456789, "A Game");

    [Theory]
    [InlineData("{title}", "A Title")]
    [InlineData("{id}", "abc123")]
    [InlineData("{channel}", "streamer8")]
    [InlineData("{date}", "11-1-84")]
    [InlineData("{trim_start}", "01-02-03")]
    [InlineData("{trim_end}", "05-06-07")]
    [InlineData("{length}", "04-04-04")]
    [InlineData("{views}", "123456789")]
    [InlineData("{game}", "A Game")]
    [InlineData("{date_custom=\"s\"}", "1984-11-01T09_43_21")]
    [InlineData("{trim_start_custom=\"hh\\-mm\\-ss\"}", "01-02-03")]
    [InlineData("{trim_end_custom=\"hh\\-mm\\-ss\"}", "05-06-07")]
    [InlineData("{length_custom=\"hh\\-mm\\-ss\"}", "04-04-04")]
    public void CorrectlyGeneratesIndividualTemplates(string template, string expected) {
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            template,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("[{date_custom=\"M-dd-yy\"}] {channel} - {title}", "[11-01-84] streamer8 - A Title")]
    [InlineData(
        "[{channel}] [{date_custom=\"M-dd-yy\"}] [{game}] {title} ({id}) - {views} views",
        "[streamer8] [11-01-84] [A Game] A Title (abc123) - 123456789 views"
    )]
    [InlineData(
        "{title} by {channel} playing {game} on {date_custom=\"M dd, yyyy\"} for {length_custom=\"h'h 'm'm 's's'\"} with {views} views",
        "A Title by streamer8 playing A Game on 11 01, 1984 for 4h 4m 4s with 123456789 views"
    )]
    public void CorrectlyGeneratesLargeTemplates(string template, string expected) {
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            template,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CorrectlyInterpretsMultipleCustomParameters() {
        const string TEMPLATE
            = "{date_custom=\"yyyy\"} {date_custom=\"MM\"} {date_custom=\"dd\"} {trim_start_custom=\"hh\\-mm\\-ss\"} {trim_end_custom=\"hh\\-mm\\-ss\"} {length_custom=\"hh\\-mm\\-ss\"}";
        const string EXPECTED = "1984 11 01 01-02-03 05-06-07 04-04-04";
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyGeneratesSubFolders_WithForwardSlash() {
        const string TEMPLATE
            = "{channel}/{date_custom=\"yyyy\"}/{date_custom=\"MM\"}/{date_custom=\"dd\"}/{title}.mp4";
        var expected = Path.Combine("streamer8", "1984", "11", "01", "A Title.mp4");
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CorrectlyGeneratesSubFolders_WithBackSlash() {
        const string TEMPLATE
            = "{channel}\\{date_custom=\"yyyy\"}\\{date_custom=\"MM\"}\\{date_custom=\"dd\"}\\{title}";
        var expected = Path.Combine("streamer8", "1984", "11", "01", "A Title");
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{title}", "＂＊：＜＞？｜／＼")]
    [InlineData("{id}", "＂＊：＜＞？｜／＼")]
    [InlineData("{channel}", "＂＊：＜＞？｜／＼")]
    [InlineData("{game}", "＂＊：＜＞？｜／＼")]
    public void CorrectlyReplacesInvalidCharactersForNonCustomTemplates(string template, string expected) {
        const string INVALID_CHARS = "\"*:<>?|/\\";

        var result = FilenameService.GetFilename(
            template,
            INVALID_CHARS,
            INVALID_CHARS,
            default,
            INVALID_CHARS,
            default,
            default,
            default,
            INVALID_CHARS
        );

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{date_custom=\"'")]
    [InlineData("{trim_start_custom=\"'")]
    [InlineData("{trim_end_custom=\"'")]
    [InlineData("{length_custom=\"'")]
    public void CorrectlyReplacesInvalidCharactersForCustomTemplates(string templateStart) {
        const string EXPECTED = "＂＊：＜＞？｜／＼";
        const string INVALID_CHARS = "\"*:<>?|/\\\\";
        var template = templateStart + INVALID_CHARS + "'\"}";

        var result = FilenameService.GetFilename(
            template,
            INVALID_CHARS,
            INVALID_CHARS,
            default,
            INVALID_CHARS,
            default,
            default,
            default,
            INVALID_CHARS
        );

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void CorrectlyReplacesInvalidCharactersForSubFolders() {
        const string INVALID_CHARS = "\"*:<>?|";
        const string FULL_WIDTH_CHARS = "＂＊：＜＞？｜";
        const string TEMPLATE = INVALID_CHARS + "\\{title}";
        var expected = Path.Combine(FULL_WIDTH_CHARS, "A Title");
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(expected, result);
    }

    [Fact]
    public void RandomStringIsRandom() {
        const string TEMPLATE = "{random_string}";
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );
        var result2 = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.NotEqual(result, result2);
    }

    [Fact]
    public void DoesNotInterpretBogusTemplateParameter() {
        const string TEMPLATE = "{foobar}";
        const string EXPECTED = "{foobar}";
        var (title, id, date, channel, trimStart, trimEnd, viewCount, game) = GetExampleInfo();

        var result = FilenameService.GetFilename(
            TEMPLATE,
            title,
            id,
            date,
            channel,
            trimStart,
            trimEnd,
            viewCount,
            game
        );

        Assert.Equal(EXPECTED, result);
    }

    [Fact]
    public void GetFilenameDoesNotThrow_WhenNullOrDefaultInput() {
        const string TEMPLATE
            = "{title}_{id}_{date}_{channel}_{trim_start}_{trim_end}_{length}_{views}_{game}_{date_custom=\"s\"}_{trim_start_custom=\"hh\\-mm\\-ss\"}_{trim_end_custom=\"hh\\-mm\\-ss\"}_{length_custom=\"hh\\-mm\\-ss\"}";
        const string EXPECTED
            = "__1-1-01__00-00-00_00-00-00_00-00-00_0__0001-01-01T00_00_00_00-00-00_00-00-00_00-00-00";

        var result = FilenameService.GetFilename(
            TEMPLATE,
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default
        );

        Assert.Equal(EXPECTED, result);
    }

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
    public void CorrectlyReplacesInvalidFilenameCharacters(string str, string expected) {
        var actual = FilenameService.ReplaceInvalidFilenameChars(str);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReplaceInvalidFilenameCharactersDoesNotThrow_WhenNullInput() {
        const string? STR = null;
        const string? EXPECTED = null;

        var actual = FilenameService.ReplaceInvalidFilenameChars(STR);

        Assert.Equal(EXPECTED, actual);
    }

    [Fact]
    public void GetNonCollidingNameWorks_WhenNoCollisionExists() {
        var expected = Path.Combine(Path.GetTempPath(), "foo.txt");
        var path = Path.Combine(Path.GetTempPath(), "foo.txt");
        var fileInfo = new FileInfo(path);

        try {
            var actual = FilenameService.GetNonCollidingName(fileInfo);

            Assert.Equal(expected, actual.FullName);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetNonCollidingNameWorks_WhenCollisionExists() {
        var expected = Path.Combine(Path.GetTempPath(), "foo (1).txt");
        var path = Path.Combine(Path.GetTempPath(), "foo.txt");
        var fileInfo = new FileInfo(path);

        try {
            fileInfo.Create().Close();

            var actual = FilenameService.GetNonCollidingName(fileInfo);

            Assert.Equal(expected, actual.FullName);
        } finally {
            File.Delete(path);
        }
    }
}
