namespace TwitchDownloaderAvalonia.Services
{
    public readonly record struct UrlListEntry(string Id, string Source, bool IsClip);

    public sealed class UrlListParseResult
    {
        public List<UrlListEntry> Entries { get; } = [];
        public List<string> Invalid { get; } = [];
    }

    public static class UrlListParser
    {
        public static UrlListParseResult Parse(string? text)
        {
            var result = new UrlListParseResult();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            var lines = text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = IdParse.MatchVideoOrClipId(line);
                if (match is not { Success: true })
                {
                    result.Invalid.Add(line);
                    continue;
                }

                result.Entries.Add(new UrlListEntry(match.Value, line, IsClip: !match.Value.All(char.IsDigit)));
            }

            return result;
        }
    }
}
