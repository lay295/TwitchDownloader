using System.Text.Json;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    public class GiphyResolverTests
    {
        [Theory]
        [InlineData("[Horse Stare GIF by Jan Metternich]", "Horse Stare GIF by Jan Metternich")]
        [InlineData("[Cry Baby Crying GIF by Peppa Pig]", "Cry Baby Crying GIF by Peppa Pig")]
        [InlineData("[bryan cranston coward GIF]", "bryan cranston coward GIF")]
        [InlineData("  [Season 4 No GIF by The Office]  ", "Season 4 No GIF by The Office")]
        public void ParsesPostedGifAltText(string messageBody, string expectedTitle)
        {
            Assert.True(GiphyResolver.TryParseAltText(messageBody, out var title));
            Assert.Equal(expectedTitle, title);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("the gif situation is out of control")]
        [InlineData("[not a gif, no keyword]")]            // bracketed, but no "GIF" token
        [InlineData("[GIFT] a bracketed word containing gif")]
        [InlineData("look at this [Horse Stare GIF] here")] // must be the whole message
        public void IgnoresMessagesThatAreNotPostedGifs(string? messageBody)
        {
            Assert.False(GiphyResolver.TryParseAltText(messageBody, out var title));
            Assert.Null(title);
        }

        /// <summary>
        /// Giphy titles are not chat text, so a word in one that happens to match an emote code is a coincidence.
        /// These must still be detected as posted GIFs, because that is what stops the renderer substituting an emote
        /// into the middle of a title and producing "[Horse &lt;emote&gt; GIF by Jan Metternich]".
        /// </summary>
        [Theory]
        [InlineData("[Horse Stare GIF by Jan Metternich]", "Horse Stare GIF by Jan Metternich")] // Stare is a real 7TV emote
        [InlineData("[Kappa GIF by Someone]", "Kappa GIF by Someone")]
        [InlineData("[PogChamp Reaction GIF]", "PogChamp Reaction GIF")]
        [InlineData("[LUL GIF by Twitch]", "LUL GIF by Twitch")]
        public void DetectsTitlesThatCollideWithEmoteCodes(string messageBody, string expectedTitle)
        {
            Assert.True(GiphyResolver.TryParseAltText(messageBody, out var title));
            Assert.Equal(expectedTitle, title);
        }

        /// <summary>An ordinary message that merely mentions an emote must keep its emotes, so it must not be detected.</summary>
        [Theory]
        [InlineData("Kappa that was great")]
        [InlineData("Kappa [Horse Stare GIF by Jan Metternich]")]
        [InlineData("[Horse Stare GIF by Jan Metternich] Kappa")]
        public void DoesNotDetectOrdinaryMessagesContainingEmotes(string messageBody)
        {
            Assert.False(GiphyResolver.TryParseAltText(messageBody, out _));
        }

        [Fact]
        public void GetTitlesDeduplicatesAndSkipsPlainMessages()
        {
            var comments = new[]
            {
                CommentWith("[Horse Stare GIF by Jan Metternich]"),
                CommentWith("1 a day sounds good"),
                CommentWith("[Horse Stare GIF by Jan Metternich]"),
                CommentWith("[Cry Baby Crying GIF by Peppa Pig]"),
                new Comment { message = null },
            };

            Assert.Equal(
                ["Horse Stare GIF by Jan Metternich", "Cry Baby Crying GIF by Peppa Pig"],
                GiphyResolver.GetTitles(comments));
        }

        [Theory]
        [InlineData("Horse Stare GIF by Jan Metternich", "horse-stare-gif-by-jan-metternich")]
        [InlineData("Aaron Paul He Can't Keep Getting Away!", "aaron-paul-he-can-t-keep-getting-away")]
        [InlineData("  spaced  out  ", "spaced-out")]
        [InlineData("!!!", "")]
        public void SlugifiesSearchQueries(string text, string expected)
        {
            Assert.Equal(expected, GiphyResolver.Slugify(text));
        }

        [Fact]
        public void ParsesResultsOutOfASearchPage()
        {
            var page = SearchPage(
                ("""{"id":"JMt40tiMr3LOKwkq0E","title":"Horse Stare GIF by Jan Metternich","images":{"original":{"url":"https://media0.giphy.com/media/v1.abc123/JMt40tiMr3LOKwkq0E/giphy.gif","size":"77943","width":"338","height":"450"},"downsized":{"url":"https://media0.giphy.com/media/v1.abc123/JMt40tiMr3LOKwkq0E/giphy.gif","size":"77943","width":"338","height":"450"}}}"""),
                // this one has a genuinely smaller downsized rendition, and a bracket in its title
                ("""{"id":"kV8P59jLf3PuDr6Ffq","title":"We [Deserve] This GIF","images":{"original":{"url":"https://media3.giphy.com/media/v1.xyz789/kV8P59jLf3PuDr6Ffq/giphy.gif","size":"8221416","width":"480","height":"480"},"downsized":{"url":"https://media3.giphy.com/media/v1.xyz789/kV8P59jLf3PuDr6Ffq/giphy-downsized.gif","size":"1235164","width":"254","height":"254"}}}"""));

            var results = GiphyResolver.ParseSearchPage(page);

            Assert.Equal(2, results.Count);

            // Equal sizes mean the original is kept, and the tracking segment is stripped either way.
            Assert.Equal("JMt40tiMr3LOKwkq0E", results[0].Id);
            Assert.Equal("Horse Stare GIF by Jan Metternich", results[0].Title);
            Assert.Equal("https://media0.giphy.com/media/JMt40tiMr3LOKwkq0E/giphy.gif", results[0].Gif.Url);
            Assert.Equal((338, 450), (results[0].Gif.Width, results[0].Gif.Height));

            // A smaller downsized rendition wins, and a bracket inside a title must not end the array scan.
            Assert.Equal("We [Deserve] This GIF", results[1].Title);
            Assert.Equal("https://media3.giphy.com/media/kV8P59jLf3PuDr6Ffq/giphy-downsized.gif", results[1].Gif.Url);
            // The advertised size must follow the rendition that was picked, since it is what proves the
            // downloaded image is the real GIF and not Giphy's "content is not available" stand-in.
            Assert.Equal((254, 254), (results[1].Gif.Width, results[1].Gif.Height));
        }

        [Theory]
        [InlineData("<html>no results here</html>")]
        [InlineData("""<script>self.__next_f.push([1,"\"initialGifs\":[{\"id\":\"broken\""])</script>""")]
        public void ReturnsNothingForPagesItCannotRead(string page)
        {
            Assert.Empty(GiphyResolver.ParseSearchPage(page));
        }

        /// <summary>
        /// Giphy can change their page shape at any time, and the parser must degrade to "no results" for anything it
        /// does not understand rather than throw, because throwing would cost the user their download or render.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("initialGifs")]                                          // key, no array
        [InlineData("initialGifs[")]                                         // array never closes
        [InlineData("initialGifs[]")]                                        // empty, unescaped
        [InlineData("initialGifs[[[[[[[[[[")]                                // unbalanced nesting
        [InlineData(@"initialGifs"":[{""id"":""a""}")]                  // truncated object
        [InlineData(@"initialGifs"":[{""id"":""a"",""title"":}]")]    // malformed value
        [InlineData(@"initialGifs"":[{""id"":123,""title"":456}]")]     // numbers where strings belong
        [InlineData(@"initialGifs"":""not an array""")]                   // wrong shape entirely
        [InlineData(@"initialGifs"":[{""id"":""a"",""title"":""t"",""images"":null}]")]
        [InlineData(@"initialGifs"":[null,null]")]
        [InlineData("<html><body>Giphy redesigned everything</body></html>")]
        [InlineData("\u0000\u0001 binary garbage \uFFFF")]
        public void NeverThrowsOnPagesItCannotUnderstand(string page)
        {
            var results = Record.Exception(() => GiphyResolver.ParseSearchPage(page));
            Assert.Null(results);
        }

        /// <summary>Truncating a good page anywhere must not throw, which covers a response cut off mid transfer.</summary>
        [Fact]
        public void NeverThrowsOnAnyTruncationOfAValidPage()
        {
            var page = SearchPage(
                ("""{"id":"abc","title":"A GIF","images":{"original":{"url":"https://media0.giphy.com/media/v1.k/abc/giphy.gif","size":"10","width":"5","height":"5"}}}"""));

            for (var length = 0; length < page.Length; length++)
            {
                var truncated = page[..length];
                var thrown = Record.Exception(() => GiphyResolver.ParseSearchPage(truncated));
                Assert.True(thrown is null, $"Threw on a page truncated to {length} chars: {thrown}");
            }
        }

        [Fact]
        public void DropsResultsMissingAnIdOrTitle()
        {
            var page = SearchPage(
                ("""{"title":"No Id GIF","images":{"original":{"url":"https://x/y.gif","size":"1","width":"1","height":"1"}}}"""),
                ("""{"id":"noTitle","images":{"original":{"url":"https://x/y.gif","size":"1","width":"1","height":"1"}}}"""),
                ("""{"id":"good","title":"Good GIF","images":{"original":{"url":"https://x/y.gif","size":"1","width":"1","height":"1"}}}"""));

            var results = GiphyResolver.ParseSearchPage(page);

            Assert.Equal("good", Assert.Single(results).Id);
        }

        [Fact]
        public void FallsBackToTheCanonicalUrlWhenNoRenditionIsUsable()
        {
            var page = SearchPage(("""{"id":"noImages","title":"Bare GIF"}"""));

            var result = Assert.Single(GiphyResolver.ParseSearchPage(page));

            Assert.Equal("https://i.giphy.com/noImages.gif", result.Gif.Url);
            // Nothing was advertised, so the download must not be rejected for a size mismatch.
            Assert.Equal((0, 0), (result.Gif.Width, result.Gif.Height));
        }

        [Fact]
        public void GetTitlesToleratesNullInput()
        {
            Assert.Empty(GiphyResolver.GetTitles(null));
            Assert.Empty(GiphyResolver.GetTitles([new Comment()]));
        }

        private static Comment CommentWith(string body) => new() { message = new Message { body = body } };

        /// <summary>Wraps <paramref name="gifsJson"/> the way Giphy does: JSON escaped inside a Next.js payload script.</summary>
        private static string SearchPage(params string[] gifsJson)
        {
            var array = $"[{string.Join(',', gifsJson)}]";
            var escaped = JsonSerializer.Serialize($"\"initialGifs\":{array}");
            return $"""<html><body><script>self.__next_f.push([1,{escaped}])</script></body></html>""";
        }
    }
}
