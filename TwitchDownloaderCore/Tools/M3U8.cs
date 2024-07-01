using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    // https://en.wikipedia.org/wiki/M3U
    // https://datatracker.ietf.org/doc/html/rfc8216
    // ReSharper disable StringLiteralTypo
    public partial record M3U8(M3U8.Metadata FileMetadata, M3U8.Stream[] Streams)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("#EXTM3U");

            if (FileMetadata?.ToString() is { Length: > 0} metadataString)
                sb.AppendLine(metadataString);

            foreach (var stream in Streams)
                sb.AppendLine(stream.ToString());

            sb.Append("#EXT-X-ENDLIST");

            return sb.ToString();
        }

        public partial record Metadata
        {
            public enum PlaylistType
            {
                Unknown,
                Vod,
                Event
            }

            internal const string PLAYLIST_TYPE_VOD = "VOD";
            internal const string PLAYLIST_TYPE_EVENT = "EVENT";

            private const string TARGET_VERSION_KEY = "#EXT-X-VERSION:";
            private const string TARGET_DURATION_KEY = "#EXT-X-TARGETDURATION:";
            private const string PLAYLIST_TYPE_KEY = "#EXT-X-PLAYLIST-TYPE:";
            private const string MEDIA_SEQUENCE_KEY = "#EXT-X-MEDIA-SEQUENCE:";
            private const string TWITCH_LIVE_SEQUENCE_KEY = "#EXT-X-TWITCH-LIVE-SEQUENCE:";
            private const string TWITCH_ELAPSED_SECS_KEY = "#EXT-X-TWITCH-ELAPSED-SECS:";
            private const string TWITCH_TOTAL_SECS_KEY = "#EXT-X-TWITCH-TOTAL-SECS:";
            private const string TWITCH_INFO_KEY = "#EXT-X-TWITCH-INFO:";

            // Generic M3U headers
            public uint Version { get; internal set; }
            public uint StreamTargetDuration { get; internal set; }
            public PlaylistType Type { get; internal set; } = PlaylistType.Unknown;
            public uint MediaSequence { get; internal set; }

            // Twitch specific
            public uint TwitchLiveSequence { get; internal set; }
            public decimal TwitchElapsedSeconds { get; internal set; }
            public decimal TwitchTotalSeconds { get; internal set; }

            // Other headers that we don't have dedicated properties for. Useful for debugging.
            private readonly List<KeyValuePair<string, string>> _unparsedValues = new();
            public IReadOnlyList<KeyValuePair<string, string>> UnparsedValues => _unparsedValues;

            public override string ToString()
            {
                var sb = new StringBuilder();
                var itemSeparator = Environment.NewLine;

                StringBuilderHelpers.AppendIfNotDefault(sb, TARGET_VERSION_KEY, Version, itemSeparator);
                StringBuilderHelpers.AppendIfNotDefault(sb, TARGET_DURATION_KEY, StreamTargetDuration, itemSeparator);
                if (Type != PlaylistType.Unknown)
                {
                    sb.Append(PLAYLIST_TYPE_KEY);
                    sb.Append(Type.AsString());
                    sb.Append(itemSeparator);
                }

                StringBuilderHelpers.AppendIfNotDefault(sb, MEDIA_SEQUENCE_KEY, MediaSequence, itemSeparator);
                StringBuilderHelpers.AppendIfNotDefault(sb, TWITCH_LIVE_SEQUENCE_KEY, TwitchLiveSequence, itemSeparator);
                StringBuilderHelpers.AppendIfNotDefault(sb, TWITCH_ELAPSED_SECS_KEY, TwitchElapsedSeconds, itemSeparator);
                StringBuilderHelpers.AppendIfNotDefault(sb, TWITCH_TOTAL_SECS_KEY, TwitchTotalSeconds, itemSeparator);

                foreach (var (key, value) in _unparsedValues)
                {
                    sb.Append(key);
                    sb.Append(value);
                    sb.Append(itemSeparator);
                }

                return sb.Length == 0 ? "" : sb.TrimEnd(itemSeparator).ToString();

            }
        }

        public partial record Stream(Stream.ExtMediaInfo MediaInfo, Stream.ExtStreamInfo StreamInfo, Stream.ExtPartInfo PartInfo, DateTimeOffset ProgramDateTime, Stream.ExtByteRange ByteRange, string Path)
        {
            public Stream(ExtMediaInfo mediaInfo, ExtStreamInfo streamInfo, string path) : this(mediaInfo, streamInfo, null, default, default, path) { }

            public Stream(ExtPartInfo partInfo, DateTimeOffset programDateTime, ExtByteRange byteRange, string path) : this(null, null, partInfo, programDateTime, byteRange, path) { }

            public bool IsPlaylist { get; } = Path.AsSpan().EndsWith(".m3u8") || Path.AsSpan().EndsWith(".m3u");

            public override string ToString()
            {
                var sb = new StringBuilder();

                if (this.MediaInfo != null)
                    sb.AppendLine(this.MediaInfo.ToString());

                if (this.StreamInfo != null)
                    sb.AppendLine(this.StreamInfo.ToString());

                if (this.PartInfo != null)
                    sb.AppendLine(this.PartInfo.ToString());

                if (this.ProgramDateTime != default) {
                    sb.Append("#EXT-X-PROGRAM-DATE-TIME:");
                    sb.AppendLine(this.ProgramDateTime.ToString("O"));
                }

                if (this.ByteRange != default)
                    sb.AppendLine(this.ByteRange.ToString());

                if (!string.IsNullOrEmpty(this.Path))
                    sb.Append(this.Path);

                return sb.Length == 0 ? "" : sb.ToString();

            }

            public readonly partial record struct ExtByteRange(uint Start, uint Length)
            {
                internal const string BYTE_RANGE_KEY = "#EXT-X-BYTERANGE:";

                public override string ToString() => $"{BYTE_RANGE_KEY}{this.Start}@{this.Length}";

                public static implicit operator ExtByteRange((uint start, uint length) tuple) => new(tuple.start, tuple.length);
            }

            public partial record ExtMediaInfo
            {
                public enum MediaType
                {
                    Unknown,
                    Video,
                    Audio
                }

                internal const string MEDIA_TYPE_VIDEO = "VIDEO";
                internal const string MEDIA_TYPE_AUDIO = "AUDIO";

                internal const string MEDIA_INFO_KEY = "#EXT-X-MEDIA:";

                private ExtMediaInfo() { }

                public ExtMediaInfo(MediaType type, string groupId, string name, bool autoSelect, bool @default)
                {
                    this.Type = type;
                    this.GroupId = groupId;
                    this.Name = name;
                    this.AutoSelect = autoSelect;
                    this.Default = @default;
                }

                public MediaType Type { get; internal set; } = MediaType.Unknown;
                public string GroupId { get; internal set; }
                public string Name { get; internal set; }
                public bool AutoSelect { get; internal set; }
                public bool Default { get; internal set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(MEDIA_INFO_KEY);
                    ReadOnlySpan<char> keyValueSeparator = stackalloc char[] { ',' };

                    if (this.Type != MediaType.Unknown)
                    {
                        sb.Append("TYPE=");
                        sb.Append(Type.AsString());
                        sb.Append(keyValueSeparator);
                    }

                    StringBuilderHelpers.AppendStringIfNotNullOrEmpty(sb, "GROUP-ID=", GroupId, keyValueSeparator);
                    StringBuilderHelpers.AppendStringIfNotNullOrEmpty(sb, "NAME=", Name, keyValueSeparator);

                    sb.Append("AUTOSELECT=");
                    sb.Append(BooleanToWord(AutoSelect));
                    sb.Append(keyValueSeparator);

                    sb.Append("DEFAULT=");
                    sb.Append(BooleanToWord(Default));

                    return sb.ToString();

                    static string BooleanToWord(bool b) => b ? "YES" : "NO";
                }
            }

            public partial record ExtStreamInfo
            {
                public readonly partial record struct StreamResolution(uint Width, uint Height)
                {
                    public override string ToString() => $"{Width}x{Height}";

                    public static implicit operator StreamResolution((uint width, uint height) tuple) => new(tuple.width, tuple.height);
                }

                internal const string STREAM_INFO_KEY = "#EXT-X-STREAM-INF:";

                private ExtStreamInfo() { }

                public ExtStreamInfo(int programId, int bandwidth, string codecs, StreamResolution resolution, string video, decimal framerate)
                {
                    this.ProgramId = programId;
                    this.Bandwidth = bandwidth;
                    this.Codecs = codecs;
                    this.Resolution = resolution;
                    this.Video = video;
                    this.Framerate = framerate;
                }

                public int ProgramId { get; internal set; }
                public int Bandwidth { get; internal set; }
                public string Codecs { get; internal set; }
                public StreamResolution Resolution { get; internal set; }
                public string Video { get; internal set; }
                public decimal Framerate { get; internal set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(STREAM_INFO_KEY);
                    ReadOnlySpan<char> keyValueSeparator = stackalloc char[] { ',' };

                    StringBuilderHelpers.AppendIfNotDefault(sb, "PROGRAM-ID=", ProgramId, keyValueSeparator);
                    StringBuilderHelpers.AppendIfNotDefault(sb, "BANDWIDTH=", Bandwidth, keyValueSeparator);
                    StringBuilderHelpers.AppendStringIfNotNullOrEmpty(sb, "CODECS=", Codecs, keyValueSeparator);
                    StringBuilderHelpers.AppendIfNotDefault(sb, "RESOLUTION=", Resolution, keyValueSeparator);
                    StringBuilderHelpers.AppendStringIfNotNullOrEmpty(sb, "VIDEO=", Video, keyValueSeparator);
                    StringBuilderHelpers.AppendIfNotDefault(sb, "FRAME-RATE=", Framerate, default);

                    return sb.ToString();
                }
            }

            public partial record ExtPartInfo
            {
                internal const string PART_INFO_KEY = "#EXTINF:";

                private ExtPartInfo() { }

                public ExtPartInfo(decimal duration, bool live)
                {
                    this.Duration = duration;
                    this.Live = live;
                }

                public decimal Duration { get; internal set; }
                public bool Live { get; internal set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(PART_INFO_KEY);

                    sb.Append(this.Duration.ToString(CultureInfo.InvariantCulture));

                    // Twitch leaves a trailing comma, so we will too.
                    sb.Append(',');

                    if (this.Live)
                        sb.Append("live");

                    return sb.ToString();
                }
            }
        }

        private static class StringBuilderHelpers
        {
            public static void AppendIfNotDefault(StringBuilder sb, string keyName, uint value, ReadOnlySpan<char> end)
            {
                if (value == default)
                    return;

                sb.Append(keyName);
                sb.Append(value);
                sb.Append(end);
            }

            public static void AppendIfNotDefault(StringBuilder sb, string keyName, int value, ReadOnlySpan<char> end)
            {
                if (value == default)
                    return;

                sb.Append(keyName);
                sb.Append(value);
                sb.Append(end);
            }

            public static void AppendIfNotDefault(StringBuilder sb, string keyName, decimal value, ReadOnlySpan<char> end)
            {
                if (value == default)
                    return;

                sb.Append(keyName);
                sb.Append(value.ToString(CultureInfo.InvariantCulture));
                sb.Append(end);
            }

            public static void AppendIfNotDefault(StringBuilder sb, string keyName, Stream.ExtStreamInfo.StreamResolution value, ReadOnlySpan<char> end)
            {
                if (value == default)
                    return;

                sb.Append(keyName);
                sb.Append(value.ToString());
                sb.Append(end);
            }

            public static void AppendStringIfNotNullOrEmpty(StringBuilder sb, string keyName, string value, ReadOnlySpan<char> end)
            {
                if (string.IsNullOrEmpty(value))
                    return;

                sb.Append(keyName);

                if (!keyName.EndsWith('"'))
                    sb.Append('"');

                sb.Append(value);
                sb.Append('"');
                sb.Append(end);
            }
        }
    }

    public static class EnumExtensions
    {
        public static string AsString(this M3U8.Stream.ExtMediaInfo.MediaType mediaType)
        {
            return mediaType switch
            {
                M3U8.Stream.ExtMediaInfo.MediaType.Unknown => null,
                M3U8.Stream.ExtMediaInfo.MediaType.Video => M3U8.Stream.ExtMediaInfo.MEDIA_TYPE_VIDEO,
                M3U8.Stream.ExtMediaInfo.MediaType.Audio => M3U8.Stream.ExtMediaInfo.MEDIA_TYPE_AUDIO,
                _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null)
            };
        }

        public static string AsString(this M3U8.Metadata.PlaylistType playlistType)
        {
            return playlistType switch
            {
                M3U8.Metadata.PlaylistType.Unknown => null,
                M3U8.Metadata.PlaylistType.Vod => M3U8.Metadata.PLAYLIST_TYPE_VOD,
                M3U8.Metadata.PlaylistType.Event => M3U8.Metadata.PLAYLIST_TYPE_EVENT,
                _ => throw new ArgumentOutOfRangeException(nameof(playlistType), playlistType, null)
            };
        }
    }
}