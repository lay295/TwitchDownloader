using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    // https://en.wikipedia.org/wiki/M3U
    // https://datatracker.ietf.org/doc/html/rfc8216
    // ReSharper disable StringLiteralTypo
    public sealed record M3U8(M3U8.Metadata FileMetadata, M3U8.Stream[] Streams)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("#EXTM3U");

            if (FileMetadata?.ToString() is { Length: > 0} metadataString)
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

        public static M3U8 Parse(System.IO.Stream stream, Encoding streamEncoding, string basePath = "")
        {
            var sr = new StreamReader(stream, streamEncoding);
            if (!ParsingHelpers.TryParseM3UHeader(sr.ReadLine(), out _))
            {
                throw new FormatException("Invalid playlist, M3U header is missing.");
            }

            var streams = new List<Stream>();

            Stream.ExtMediaInfo currentExtMediaInfo = null;
            Stream.ExtStreamInfo currentExtStreamInfo = null;

            Metadata.Builder metadataBuilder = new();
            DateTimeOffset currentExtProgramDateTime = default;
            Stream.ExtByteRange currentByteRange = default;
            Stream.ExtPartInfo currentExtPartInfo = null;

            while (sr.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    ClearStreamMetadata(out currentExtMediaInfo, out currentExtStreamInfo, out currentExtProgramDateTime, out currentByteRange, out currentExtPartInfo);
                    continue;
                }

                if (line[0] != '#')
                {
                    var path = Path.Combine(basePath, line);
                    streams.Add(new Stream(currentExtMediaInfo, currentExtStreamInfo, currentExtPartInfo, currentExtProgramDateTime, currentByteRange, path));
                    ClearStreamMetadata(out currentExtMediaInfo, out currentExtStreamInfo, out currentExtProgramDateTime, out currentByteRange, out currentExtPartInfo);

                    continue;
                }

                if (!ParseM3U8Key(line, metadataBuilder, ref currentExtMediaInfo, ref currentExtStreamInfo, ref currentExtProgramDateTime, ref currentByteRange, ref currentExtPartInfo))
                {
                    break;
                }
            }

            return new M3U8(metadataBuilder.ToMetadata(), streams.ToArray());
        }

        public static M3U8 Parse(ReadOnlySpan<char> text, string basePath = "")
        {
            if (!ParsingHelpers.TryParseM3UHeader(text, out text))
            {
                throw new FormatException("Invalid playlist, M3U header is missing.");
            }

            var streams = new List<Stream>();

            Stream.ExtMediaInfo currentExtMediaInfo = null;
            Stream.ExtStreamInfo currentExtStreamInfo = null;

            Metadata.Builder metadataBuilder = new();
            DateTimeOffset currentExtProgramDateTime = default;
            Stream.ExtByteRange currentByteRange = default;
            Stream.ExtPartInfo currentExtPartInfo = null;

            var textStart = -1;
            var textEnd = text.Length;
            var lineEnd = -1;
            var iterations = 0;
            var maxIterations = text.Count('\n') + 1;
            do
            {
                textStart++;
                iterations++;
                if (iterations > maxIterations)
                    throw new Exception("Infinite loop encountered while decoding M3U8 playlist.");

                if (textStart >= textEnd)
                    break;

                var workingSlice = text[textStart..];
                lineEnd = workingSlice.IndexOf('\n');
                if (lineEnd != -1)
                    workingSlice = workingSlice[..lineEnd].TrimEnd('\r');

                if (workingSlice.IsWhiteSpace())
                {
                    ClearStreamMetadata(out currentExtMediaInfo, out currentExtStreamInfo, out currentExtProgramDateTime, out currentByteRange, out currentExtPartInfo);
                    continue;
                }

                if (workingSlice[0] != '#')
                {
                    var path = Path.Combine(basePath, workingSlice.ToString());
                    streams.Add(new Stream(currentExtMediaInfo, currentExtStreamInfo, currentExtPartInfo, currentExtProgramDateTime, currentByteRange, path));
                    ClearStreamMetadata(out currentExtMediaInfo, out currentExtStreamInfo, out currentExtProgramDateTime, out currentByteRange, out currentExtPartInfo);

                    if (lineEnd == -1)
                        break;

                    continue;
                }

                if (!ParseM3U8Key(workingSlice, metadataBuilder, ref currentExtMediaInfo, ref currentExtStreamInfo, ref currentExtProgramDateTime, ref currentByteRange, ref currentExtPartInfo))
                {
                    break;
                }

                if (lineEnd == -1)
                {
                    break;
                }
            } while ((textStart += lineEnd) < textEnd);

            return new M3U8(metadataBuilder.ToMetadata(), streams.ToArray());
        }

        private static void ClearStreamMetadata(out Stream.ExtMediaInfo currentExtMediaInfo, out Stream.ExtStreamInfo currentExtStreamInfo, out DateTimeOffset currentExtProgramDateTime,
            out Stream.ExtByteRange currentByteRange, out Stream.ExtPartInfo currentExtPartInfo)
        {
            currentExtMediaInfo = null;
            currentExtStreamInfo = null;
            currentExtProgramDateTime = default;
            currentByteRange = default;
            currentExtPartInfo = null;
        }

        private static bool ParseM3U8Key(ReadOnlySpan<char> text, Metadata.Builder metadataBuilder, ref Stream.ExtMediaInfo extMediaInfo, ref Stream.ExtStreamInfo extStreamInfo,
            ref DateTimeOffset extProgramDateTime, ref Stream.ExtByteRange byteRange, ref Stream.ExtPartInfo extPartInfo)
        {
            const string PROGRAM_DATE_TIME_KEY = "#EXT-X-PROGRAM-DATE-TIME:";
            const string END_LIST_KEY = "#EXT-X-ENDLIST";
            if (text.StartsWith(Stream.ExtMediaInfo.MEDIA_INFO_KEY))
            {
                extMediaInfo = Stream.ExtMediaInfo.Parse(text);
            }
            else if (text.StartsWith(Stream.ExtStreamInfo.STREAM_INFO_KEY))
            {
                extStreamInfo = Stream.ExtStreamInfo.Parse(text);
            }
            else if (text.StartsWith(PROGRAM_DATE_TIME_KEY))
            {
                extProgramDateTime = ParsingHelpers.ParseDateTimeOffset(text, PROGRAM_DATE_TIME_KEY, false);
            }
            else if (text.StartsWith(Stream.ExtByteRange.BYTE_RANGE_KEY))
            {
                byteRange = Stream.ExtByteRange.Parse(text);
            }
            else if (text.StartsWith(Stream.ExtPartInfo.PART_INFO_KEY))
            {
                extPartInfo = Stream.ExtPartInfo.Parse(text);
            }
            else if (text.StartsWith(END_LIST_KEY))
            {
                return false;
            }
            else
            {
                metadataBuilder.ParseAndAppend(text);
            }

            return true;
        }

        public sealed record Metadata
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
            public uint Version { get; private set; }
            public uint StreamTargetDuration { get; private set; }
            public PlaylistType Type { get; private set; } = PlaylistType.Unknown;
            public uint MediaSequence { get; private set; }

            // Twitch specific
            public uint TwitchLiveSequence { get; private set; }
            public decimal TwitchElapsedSeconds { get; private set; }
            public decimal TwitchTotalSeconds { get; private set; }

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

                if (sb.Length == 0)
                {
                    return "";
                }

                return sb.TrimEnd(itemSeparator).ToString();
            }

            public sealed class Builder
            {
                private Metadata _metadata;

                public Builder ParseAndAppend(ReadOnlySpan<char> text)
                {
                    text = text.Trim();

                    if (!text.IsEmpty)
                    {
                        ParseAndAppendCore(text);
                    }

                    return this;
                }

                private void ParseAndAppendCore(ReadOnlySpan<char> text)
                {
                    if (text.StartsWith(TARGET_VERSION_KEY))
                    {
                        _metadata ??= new Metadata();
                        _metadata.Version = ParsingHelpers.ParseUIntValue(text, TARGET_VERSION_KEY);
                    }
                    else if (text.StartsWith(TARGET_DURATION_KEY))
                    {
                        _metadata ??= new Metadata();
                        _metadata.StreamTargetDuration = ParsingHelpers.ParseUIntValue(text, TARGET_DURATION_KEY);
                    }
                    else if (text.StartsWith(PLAYLIST_TYPE_KEY))
                    {
                        _metadata ??= new Metadata();
                        var temp = text[PLAYLIST_TYPE_KEY.Length..];
                        if (temp.StartsWith(PLAYLIST_TYPE_VOD))
                            _metadata.Type = PlaylistType.Vod;
                        else if (temp.StartsWith(PLAYLIST_TYPE_EVENT))
                            _metadata.Type = PlaylistType.Event;
                        else
                            throw new FormatException($"Unable to parse PlaylistType from: {text}");
                    }
                    else if (text.StartsWith(MEDIA_SEQUENCE_KEY))
                    {
                        _metadata ??= new Metadata();
                        _metadata.MediaSequence = ParsingHelpers.ParseUIntValue(text, MEDIA_SEQUENCE_KEY);
                    }
                    else if (text.StartsWith(TWITCH_LIVE_SEQUENCE_KEY))
                    {
                        _metadata ??= new Metadata();
                        _metadata.TwitchLiveSequence = ParsingHelpers.ParseUIntValue(text, TWITCH_LIVE_SEQUENCE_KEY);
                    }
                    else if (text.StartsWith(TWITCH_ELAPSED_SECS_KEY))
                    {
                        _metadata ??= new Metadata();
                        _metadata.TwitchElapsedSeconds = ParsingHelpers.ParseDecimalValue(text, TWITCH_ELAPSED_SECS_KEY);
                    }
                    else if (text.StartsWith(TWITCH_TOTAL_SECS_KEY))
                    {
                        _metadata ??= new Metadata();
                        _metadata.TwitchTotalSeconds = ParsingHelpers.ParseDecimalValue(text, TWITCH_TOTAL_SECS_KEY);
                    }
                    else if (text.StartsWith(TWITCH_INFO_KEY))
                    {
                        // Do nothing. This header includes response related info that we don't need.
                    }
                    else if (text[0] == '#')
                    {
                        _metadata ??= new Metadata();
                        var colonIndex = text.IndexOf(':');
                        if (colonIndex != -1)
                        {
                            var kvp = new KeyValuePair<string, string>(text[..(colonIndex + 1)].ToString(), text[(colonIndex + 1)..].ToString());
                            _metadata._unparsedValues.Add(kvp);
                        }
                        else
                        {
                            var kvp = new KeyValuePair<string, string>("", text.ToString());
                            _metadata._unparsedValues.Add(kvp);
                        }
                    }
                }

                public Metadata ToMetadata()
                {
                    return _metadata;
                }
            }
        }

        public sealed record Stream(Stream.ExtMediaInfo MediaInfo, Stream.ExtStreamInfo StreamInfo, Stream.ExtPartInfo PartInfo, DateTimeOffset ProgramDateTime, Stream.ExtByteRange ByteRange, string Path)
        {
            public Stream(ExtMediaInfo mediaInfo, ExtStreamInfo streamInfo, string path) : this(mediaInfo, streamInfo, null, default, default, path) { }

            public Stream(ExtPartInfo partInfo, DateTimeOffset programDateTime, ExtByteRange byteRange, string path) : this(null, null, partInfo, programDateTime, byteRange, path) { }

            public bool IsPlaylist { get; } = Path.AsSpan().EndsWith(".m3u8") || Path.AsSpan().EndsWith(".m3u");

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
                {
                    sb.Append("#EXT-X-PROGRAM-DATE-TIME:");
                    sb.AppendLine(ProgramDateTime.ToString("O"));
                }

                if (ByteRange != default)
                    sb.AppendLine(ByteRange.ToString());

                if (!string.IsNullOrEmpty(Path))
                    sb.Append(Path);

                if (sb.Length == 0)
                    return "";

                return sb.ToString();
            }

            public readonly record struct ExtByteRange(uint Start, uint Length)
            {
                internal const string BYTE_RANGE_KEY = "#EXT-X-BYTERANGE:";

                public override string ToString() => $"{BYTE_RANGE_KEY}{Start}@{Length}";

                public static ExtByteRange Parse(ReadOnlySpan<char> text)
                {
                    if (text.StartsWith(BYTE_RANGE_KEY))
                        text = text[17..];

                    var separatorIndex = text.IndexOf('@');
                    if (separatorIndex == -1)
                        throw new FormatException($"Unable to parse ByteRange from {text}.");

                    if (!uint.TryParse(text[..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
                        throw new FormatException($"Unable to parse ByteRange from {text}.");

                    if (!uint.TryParse(text[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
                        throw new FormatException($"Unable to parse ByteRange from {text}.");

                    return new ExtByteRange(start, end);
                }

                public static implicit operator ExtByteRange((uint start, uint length) tuple) => new(tuple.start, tuple.length);
            }

            public sealed class ExtMediaInfo
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

                public MediaType Type { get; private set; } = MediaType.Unknown;
                public string GroupId { get; private set; }
                public string Name { get; private set; }
                public bool AutoSelect { get; private set; }
                public bool Default { get; private set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(MEDIA_INFO_KEY);
                    ReadOnlySpan<char> keyValueSeparator = stackalloc char[] { ',' };

                    if (Type != MediaType.Unknown)
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

                    static string BooleanToWord(bool b)
                    {
                        return b ? "YES" : "NO";
                    }
                }

                public static ExtMediaInfo Parse(ReadOnlySpan<char> text)
                {
                    var mediaInfo = new ExtMediaInfo();

                    if (text.StartsWith(MEDIA_INFO_KEY))
                        text = text[13..];

                    const string KEY_TYPE = "TYPE=";
                    const string KEY_GROUP_ID = "GROUP-ID=\"";
                    const string KEY_NAME = "NAME=\"";
                    const string KEY_AUTOSELECT = "AUTOSELECT=";
                    const string KEY_DEFAULT = "DEFAULT=";
                    do
                    {
                        text = text.TrimStart();

                        if (text.StartsWith(KEY_TYPE))
                        {
                            var temp = text[KEY_TYPE.Length..];
                            if (temp.StartsWith(MEDIA_TYPE_VIDEO))
                                mediaInfo.Type = MediaType.Video;
                            else if (temp.StartsWith(MEDIA_TYPE_AUDIO))
                                mediaInfo.Type = MediaType.Audio;
                            else
                                throw new FormatException($"Unable to parse MediaType from: {text}");
                        }
                        else if (text.StartsWith(KEY_GROUP_ID))
                        {
                            mediaInfo.GroupId = ParsingHelpers.ParseStringValue(text, KEY_GROUP_ID);
                        }
                        else if (text.StartsWith(KEY_NAME))
                        {
                            mediaInfo.Name = ParsingHelpers.ParseStringValue(text, KEY_NAME);
                        }
                        else if (text.StartsWith(KEY_AUTOSELECT))
                        {
                            mediaInfo.AutoSelect = ParsingHelpers.ParseBooleanValue(text, KEY_AUTOSELECT);
                        }
                        else if (text.StartsWith(KEY_DEFAULT))
                        {
                            mediaInfo.Default = ParsingHelpers.ParseBooleanValue(text, KEY_DEFAULT);
                        }

                        var nextIndex = text.UnEscapedIndexOf(',');
                        if (nextIndex == -1)
                            break;

                        text = text[(nextIndex + 1)..];
                    } while (true);

                    return mediaInfo;
                }
            }

            public sealed record ExtStreamInfo
            {
                public readonly record struct StreamResolution(uint Width, uint Height)
                {
                    public override string ToString() => $"{Width}x{Height}";

                    public static StreamResolution Parse(ReadOnlySpan<char> text)
                    {
                        if (text.StartsWith("RESOLUTION="))
                            text = text[11..];

                        var separatorIndex = text.IndexOfAny("x");
                        if (separatorIndex == -1 || separatorIndex == text.Length)
                            throw new FormatException($"Unable to parse Resolution from {text}.");

                        if (!uint.TryParse(text[..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width))
                            throw new FormatException($"Unable to parse Resolution from {text}.");

                        if (!uint.TryParse(text[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
                            throw new FormatException($"Unable to parse Resolution from {text}.");

                        return new StreamResolution(width, height);
                    }

                    public static implicit operator StreamResolution((uint width, uint height) tuple) => new(tuple.width, tuple.height);
                }

                internal const string STREAM_INFO_KEY = "#EXT-X-STREAM-INF:";

                private ExtStreamInfo() { }

                public ExtStreamInfo(int programId, int bandwidth, string codecs, StreamResolution resolution, string video, decimal framerate)
                {
                    ProgramId = programId;
                    Bandwidth = bandwidth;
                    Codecs = codecs;
                    Resolution = resolution;
                    Video = video;
                    Framerate = framerate;
                }

                public int ProgramId { get; private set; }
                public int Bandwidth { get; private set; }
                public string Codecs { get; private set; }
                public StreamResolution Resolution { get; private set; }
                public string Video { get; private set; }
                public decimal Framerate { get; private set; }

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

                public static ExtStreamInfo Parse(ReadOnlySpan<char> text)
                {
                    var streamInfo = new ExtStreamInfo();

                    if (text.StartsWith(STREAM_INFO_KEY))
                        text = text[18..];

                    const string KEY_PROGRAM_ID = "PROGRAM-ID=";
                    const string KEY_BANDWIDTH = "BANDWIDTH=";
                    const string KEY_CODECS = "CODECS=\"";
                    const string KEY_RESOLUTION = "RESOLUTION=";
                    const string KEY_VIDEO = "VIDEO=\"";
                    const string KEY_FRAMERATE = "FRAME-RATE=";
                    do
                    {
                        text = text.TrimStart();

                        if (text.StartsWith(KEY_PROGRAM_ID))
                        {
                            streamInfo.ProgramId = ParsingHelpers.ParseIntValue(text, KEY_PROGRAM_ID, false);
                        }
                        else if (text.StartsWith(KEY_BANDWIDTH))
                        {
                            streamInfo.Bandwidth = ParsingHelpers.ParseIntValue(text, KEY_BANDWIDTH, false);
                        }
                        else if (text.StartsWith(KEY_CODECS))
                        {
                            streamInfo.Codecs = ParsingHelpers.ParseStringValue(text, KEY_CODECS);
                        }
                        else if (text.StartsWith(KEY_RESOLUTION))
                        {
                            streamInfo.Resolution = ParsingHelpers.ParseResolution(text, KEY_RESOLUTION);
                        }
                        else if (text.StartsWith(KEY_VIDEO))
                        {
                            streamInfo.Video = ParsingHelpers.ParseStringValue(text, KEY_VIDEO);
                        }
                        else if (text.StartsWith(KEY_FRAMERATE))
                        {
                            streamInfo.Framerate = ParsingHelpers.ParseDecimalValue(text, KEY_FRAMERATE, false);
                        }

                        var nextIndex = text.UnEscapedIndexOf(',');
                        if (nextIndex == -1)
                            break;

                        text = text[(nextIndex + 1)..];
                    } while (true);

                    // Sometimes Twitch's M3U8 response lacks a Framerate value, among other things. We can just guess the framerate using the Video value.
                    if (streamInfo.Framerate == 0 && Regex.IsMatch(streamInfo.Video, @"p\d+$", RegexOptions.RightToLeft))
                    {
                        var index = streamInfo.Video.LastIndexOf('p');
                        streamInfo.Framerate = int.Parse(streamInfo.Video.AsSpan(index + 1));
                    }

                    return streamInfo;
                }
            }

            public sealed record ExtPartInfo
            {
                internal const string PART_INFO_KEY = "#EXTINF:";

                private ExtPartInfo() { }

                public ExtPartInfo(decimal duration, bool live)
                {
                    Duration = duration;
                    Live = live;
                }

                public decimal Duration { get; private set; }
                public bool Live { get; private set; }

                public override string ToString()
                {
                    var sb = new StringBuilder(PART_INFO_KEY);

                    sb.Append(Duration.ToString(CultureInfo.InvariantCulture));

                    // Twitch leaves a trailing comma, so we will too.
                    sb.Append(',');

                    if (Live)
                    {
                        sb.Append("live");
                    }

                    return sb.ToString();
                }

                public static ExtPartInfo Parse(ReadOnlySpan<char> text)
                {
                    var partInfo = new ExtPartInfo();

                    if (text.StartsWith(PART_INFO_KEY))
                        text = text[8..];

                    do
                    {
                        text = text.TrimStart();

                        if (!text.IsEmpty && char.IsDigit(text[0]))
                        {
                            partInfo.Duration = ParsingHelpers.ParseDecimalValue(text, "");
                        }
                        else if (text.StartsWith("live"))
                        {
                            partInfo.Live = true;
                        }

                        var nextIndex = text.UnEscapedIndexOf(',');
                        if (nextIndex == -1)
                            break;

                        text = text[(nextIndex + 1)..];
                    } while (true);

                    return partInfo;
                }
            }
        }

        private static class ParsingHelpers
        {
            public static bool TryParseM3UHeader(ReadOnlySpan<char> text, out ReadOnlySpan<char> textWithoutHeader)
            {
                const string M3U_HEADER = "#EXTM3U";
                if (!text.StartsWith(M3U_HEADER))
                {
                    textWithoutHeader = default;
                    return false;
                }

                textWithoutHeader = text[7..].TrimStart(" \r\n");
                return true;
            }

            public static string ParseStringValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];

                if (temp.Contains("\\\"", StringComparison.Ordinal))
                {
                    throw new NotSupportedException("Escaped quotes are not supported. Please report this as a bug: https://github.com/lay295/TwitchDownloader/issues/new/choose");
                }

                var closeQuote = temp.IndexOf('"');
                if (closeQuote == -1)
                {
                    throw new FormatException("Expected close quote was not found.");
                }

                return temp[..closeQuote].ToString();
            }

            public static int ParseIntValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName, bool strict = true)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (int.TryParse(temp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                    return intValue;

                if (!strict)
                    return default;

                throw new FormatException($"Unable to parse integer from: {text}");
            }

            public static uint ParseUIntValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName, bool strict = true)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (uint.TryParse(temp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uIntValue))
                    return uIntValue;

                if (!strict)
                    return default;

                throw new FormatException($"Unable to parse integer from: {text}");
            }

            public static decimal ParseDecimalValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName, bool strict = true)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (decimal.TryParse(temp, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                    return decimalValue;

                if (!strict)
                    return default;

                throw new FormatException($"Unable to parse decimal from: {text}");
            }

            public static bool ParseBooleanValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName, bool strict = true)
            {
                var temp = text[keyName.Length..];

                if (temp.StartsWith("NO"))
                    return false;

                if (temp.StartsWith("YES"))
                    return true;

                temp = temp[..NextKeyStart(temp)];

                if (bool.TryParse(temp, out var booleanValue))
                    return booleanValue;

                if (!strict)
                    return default;

                throw new FormatException($"Unable to parse boolean from: {text}");
            }

            public static Stream.ExtStreamInfo.StreamResolution ParseResolution(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                return Stream.ExtStreamInfo.StreamResolution.Parse(temp);
            }

            public static DateTimeOffset ParseDateTimeOffset(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName, bool strict = true)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (DateTimeOffset.TryParse(temp, null, DateTimeStyles.AssumeUniversal, out var dateTimeOffset))
                    return dateTimeOffset;

                if (!strict)
                    return default;

                throw new FormatException($"Unable to parse DateTimeOffset from: {text}");
            }

            private static Index NextKeyStart(ReadOnlySpan<char> text)
            {
                var nextKey = text.UnEscapedIndexOfAny(",\r\n");
                return nextKey switch
                {
                    -1 => text.Length, // This is probably the last value
                    _ => nextKey
                };
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
                {
                    sb.Append('"');
                }
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