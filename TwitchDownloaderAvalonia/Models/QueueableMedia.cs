namespace TwitchDownloaderAvalonia.Models
{
    public sealed class QueueableMedia
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required DateTime Time { get; init; }
        public required int Length { get; init; }
        public required int Views { get; init; }
        public required string Game { get; init; }
        public required bool IsClip { get; init; }
        public required string StreamerName { get; init; }
        public required string StreamerId { get; init; }
        public string ClipperName { get; init; } = string.Empty;
        public string ClipperId { get; init; } = string.Empty;
        public byte[]? ThumbnailBytes { get; init; }

        public static QueueableMedia FromSearch(SearchResultItem item) => new()
        {
            Id = item.Id,
            Title = item.Title,
            Time = item.Time,
            Length = item.Length,
            Views = item.Views,
            Game = item.Game,
            IsClip = item.IsClip,
            StreamerName = item.StreamerName,
            StreamerId = item.StreamerId,
            ClipperName = item.ClipperName,
            ClipperId = item.ClipperId,
            ThumbnailBytes = item.ThumbnailBytes,
        };
    }
}
