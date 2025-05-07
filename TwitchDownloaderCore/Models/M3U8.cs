using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Models
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

            if (FileMetadata?.ToString() is { Length: > 0 } metadataString)
            {
                sb.AppendLine(metadataString);
            }

            foreach (var stream in Streams)
            {
                sb.AppendLine(stream.ToString());
            }

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
            private const string MAP_KEY = "#EXT-X-MAP:";
            private const string TWITCH_LIVE_SEQUENCE_KEY = "#EXT-X-TWITCH-LIVE-SEQUENCE:";
            private const string TWITCH_ELAPSED_SECS_KEY = "#EXT-X-TWITCH-ELAPSED-SECS:";
            private const string TWITCH_TOTAL_SECS_KEY = "#EXT-X-TWITCH-TOTAL-SECS:";
            private const string TWITCH_INFO_KEY = "#EXT-X-TWITCH-INFO:";

            // Generic M3U headers
            public uint? Version { get; init; }
            public uint? StreamTargetDuration { get; init; }
            public PlaylistType? Type { get; init; }
            public uint? MediaSequence { get; init; }
            public ExtMap Map { get; init; }

            // Twitch specific
            public uint? TwitchLiveSequence { get; init; }
            public decimal? TwitchElapsedSeconds { get; init; }
            public decimal? TwitchTotalSeconds { get; init; }

            // Other headers that we don't have dedicated properties for. Useful for debugging.
            private List<KeyValuePair<string, string>> _unparsedValues = new();
            public IReadOnlyList<KeyValuePair<string, string>> UnparsedValues => _unparsedValues;

            public override string ToString()
            {
                var sb = new StringBuilder();
                var itemSeparator = Environment.NewLine;

                if (Version.HasValue)
                    sb.AppendKeyValue(TARGET_VERSION_KEY, Version.Value, itemSeparator);

                if (StreamTargetDuration.HasValue)
                    sb.AppendKeyValue(TARGET_DURATION_KEY, StreamTargetDuration.Value, itemSeparator);

                if (Type.HasValue)
                    sb.AppendKeyValue(PLAYLIST_TYPE_KEY, Type.Value.AsString(), itemSeparator);

                if (MediaSequence.HasValue)
                    sb.AppendKeyValue(MEDIA_SEQUENCE_KEY, MediaSequence.Value, itemSeparator);

                if (TwitchLiveSequence.HasValue)
                    sb.AppendKeyValue(TWITCH_LIVE_SEQUENCE_KEY, TwitchLiveSequence.Value, itemSeparator);

                if (TwitchElapsedSeconds.HasValue)
                    sb.AppendKeyValue(TWITCH_ELAPSED_SECS_KEY, TwitchElapsedSeconds.Value, itemSeparator);

                if (TwitchTotalSeconds.HasValue)
                    sb.AppendKeyValue(TWITCH_TOTAL_SECS_KEY, TwitchTotalSeconds.Value, itemSeparator);

                if (Map is not null)
                    sb.AppendKeyValue(MAP_KEY, Map.ToString(), itemSeparator);

                foreach (var (key, value) in _unparsedValues)
                {
                    sb.AppendKeyValue(key, value, itemSeparator);
                }

                if (sb.Length == 0)
                {
                    return "";
                }

                return sb.TrimEnd(itemSeparator).ToString();
            }

            public partial record ExtMap(string Uri, ByteRange ByteRange)
            {
                private const string URI_KEY = "URI=\"";
                private const string BYTE_RANGE_KEY = "BYTERANGE=";

                public override string ToString()
                {
                    var sb = new StringBuilder();
                    ReadOnlySpan<char> itemSeparator = stackalloc char[] { ',' };

                    if (!string.IsNullOrWhiteSpace(Uri))
                        sb.AppendKeyQuoteValue(URI_KEY, Uri, itemSeparator);

                    if (ByteRange != default)
                        sb.AppendKeyQuoteValue(BYTE_RANGE_KEY, ByteRange.ToString(), itemSeparator);

                    if (sb.Length == 0)
                        return "";

                    return sb.TrimEnd(itemSeparator).ToString();
                }
            }
        }

        public partial record Stream(Stream.ExtMediaInfo MediaInfo, Stream.ExtStreamInfo StreamInfo, Stream.ExtPartInfo PartInfo, DateTimeOffset ProgramDateTime, ByteRange ByteRange, string Path)
        {
            public Stream(ExtMediaInfo mediaInfo, ExtStreamInfo streamInfo, string path) : this(mediaInfo, streamInfo, null, default, default, path) { }

            public Stream(ExtPartInfo partInfo, DateTimeOffset programDateTime, ByteRange byteRange, string path) : this(null, null, partInfo, programDateTime, byteRange, path) { }

            public bool IsPlaylist { get; } = Path.AsSpan().EndsWith(".m3u8") || Path.AsSpan().EndsWith(".m3u");

            internal const string PROGRAM_DATE_TIME_KEY = "#EXT-X-PROGRAM-DATE-TIME:";
            internal const string BYTE_RANGE_KEY = "#EXT-X-BYTERANGE:";

            public override string ToString()
            {
                var sb = new StringBuilder();

                if (MediaInfo != null)
                    sb.AppendLine(MediaInfo.ToString());

                if (StreamInfo != null)
                    sb.AppendLine(StreamInfo.ToString());

                if (PartInfo != null)
                    sb.AppendLine(PartInfo.ToString());

                if (ProgramDateTime != default)
                    sb.AppendKeyValue(PROGRAM_DATE_TIME_KEY, ProgramDateTime.ToString("O"), Environment.NewLine);

                if (ByteRange != default)
                    sb.AppendKeyValue(BYTE_RANGE_KEY, ByteRange.ToString(), Environment.NewLine);

                if (!string.IsNullOrEmpty(Path))
                    sb.Append(Path);

                if (sb.Length == 0)
                    return "";

                return sb.ToString();
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
                    Type = type;
                    GroupId = groupId;
                    Name = name;
                    AutoSelect = autoSelect;
                    Default = @default;
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

                    if (Type != MediaType.Unknown)
                        sb.AppendKeyValue("TYPE=", Type.AsString(), keyValueSeparator);

                    if (!string.IsNullOrWhiteSpace(GroupId))
                        sb.AppendKeyQuoteValue("GROUP-ID=", GroupId, keyValueSeparator);

                    if (!string.IsNullOrWhiteSpace(Name))
                        sb.AppendKeyQuoteValue("NAME=", Name, keyValueSeparator);

                    sb.AppendKeyValue("AUTOSELECT=", BooleanToWord(AutoSelect), keyValueSeparator);

                    sb.AppendKeyValue("DEFAULT=", BooleanToWord(Default), default);

                    return sb.ToString();

                    static string BooleanToWord(bool b)
                    {
                        return b ? "YES" : "NO";
                    }
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

                public ExtStreamInfo(int programId, int bandwidth, string[] codecs, StreamResolution resolution, string video, decimal framerate)
                {
                    ProgramId = programId;
                    Bandwidth = bandwidth;
                    Codecs = codecs ?? Array.Empty<string>();
                    Resolution = resolution;
                    Video = video;
                    Framerate = framerate;
                }

                public int ProgramId { get; internal set; }
                public int Bandwidth { get; internal set; }
                public IReadOnlyList<string> Codecs { get; internal set; } = Array.Empty<string>();
                public StreamResolution Resolution { get; internal set; }
                public string Video { get; internal set; }
                public decimal Framerate { get; internal set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(STREAM_INFO_KEY);
                    ReadOnlySpan<char> keyValueSeparator = stackalloc char[] { ',' };

                    if (ProgramId != default)
                        sb.AppendKeyValue("PROGRAM-ID=", ProgramId, keyValueSeparator);

                    if (Bandwidth != default)
                        sb.AppendKeyValue("BANDWIDTH=", Bandwidth, keyValueSeparator);

                    if (Codecs is { Count: > 0 })
                        sb.AppendKeyQuoteValue("CODECS=", string.Join(',', Codecs), keyValueSeparator);

                    if (Resolution != default)
                        sb.AppendKeyValue("RESOLUTION=", Resolution, keyValueSeparator);

                    if (!string.IsNullOrWhiteSpace(Video))
                        sb.AppendKeyQuoteValue("VIDEO=", Video, keyValueSeparator);

                    if (Framerate != default)
                        sb.AppendKeyValue("FRAME-RATE=", Framerate, default);

                    return sb.TrimEnd(keyValueSeparator).ToString();
                }
            }

            public partial record ExtPartInfo
            {
                internal const string PART_INFO_KEY = "#EXTINF:";

                private ExtPartInfo() { }

                public ExtPartInfo(decimal duration, bool live)
                {
                    Duration = duration;
                    Live = live;
                }

                public decimal Duration { get; internal set; }
                public bool Live { get; internal set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(PART_INFO_KEY);

                    sb.Append(Duration.ToString(CultureInfo.InvariantCulture));

                    // Twitch leaves a trailing comma, so we will too.
                    sb.Append(',');

                    if (Live)
                        sb.Append("live");

                    return sb.ToString();
                }
            }
        }

        public readonly partial record struct ByteRange(uint Length, uint Start)
        {
            public override string ToString()
            {
                if (this == default)
                    return "";

                return $"{Length}@{Start}";
            }

            public static implicit operator ByteRange((uint length, uint start) tuple) => new(tuple.length, tuple.start);
        }
    }

    internal static class StringBuilderExtensions
    {
        public static void AppendKeyValue(this StringBuilder sb, string keyName, int value, ReadOnlySpan<char> end)
        {
            sb.Append(keyName);
            sb.Append(value);
            sb.Append(end);
        }

        public static void AppendKeyValue(this StringBuilder sb, string keyName, decimal value, ReadOnlySpan<char> end)
        {
            sb.Append(keyName);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            sb.Append(end);
        }

        public static void AppendKeyValue(this StringBuilder sb, string keyName, M3U8.Stream.ExtStreamInfo.StreamResolution value, ReadOnlySpan<char> end)
        {
            sb.Append(keyName);
            sb.Append(value.ToString());
            sb.Append(end);
        }

        public static void AppendKeyValue(this StringBuilder sb, string keyName, string value, ReadOnlySpan<char> end)
        {
            sb.Append(keyName);
            sb.Append(value);
            sb.Append(end);
        }

        public static void AppendKeyQuoteValue(this StringBuilder sb, string keyName, string value, ReadOnlySpan<char> end)
        {
            sb.Append(keyName);

            if (!keyName.EndsWith('"'))
            {
                sb.Append('"');
            }

            sb.Append(value);
            sb.Append('"');
            sb.Append(end);
        }
    }

    internal static class EnumExtensions
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