﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    // https://en.wikipedia.org/wiki/M3U
    // ReSharper disable StringLiteralTypo
    public sealed record M3U8(M3U8.Metadata FileMetadata, M3U8.Stream[] Streams)
    {
        public static M3U8 Parse(ReadOnlySpan<char> text, string basePath = "")
        {
            if (!ParsingHelpers.TryParseM3UHeader(ref text))
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
                    workingSlice = workingSlice[..lineEnd];

                if (workingSlice[0] != '#')
                {
                    var path = Path.Combine(basePath, workingSlice.ToString());
                    streams.Add(new Stream(currentExtMediaInfo, currentExtStreamInfo, currentExtPartInfo, currentExtProgramDateTime, currentByteRange, path));
                    currentExtMediaInfo = null;
                    currentExtStreamInfo = null;
                    currentExtProgramDateTime = default;
                    currentByteRange = default;
                    currentExtPartInfo = null;

                    if (lineEnd == -1)
                        break;

                    continue;
                }

                const string MEDIA_INFO_KEY = "#EXT-X-MEDIA:";
                const string STREAM_INFO_KEY = "#EXT-X-STREAM-INF:";
                const string PROGRAM_DATE_TIME_KEY = "#EXT-X-PROGRAM-DATE-TIME:";
                const string BYTE_RANGE_KEY = "#EXT-X-BYTERANGE:";
                const string PART_INFO_KEY = "#EXTINF:";
                const string END_LIST_KEY = "#EXT-X-ENDLIST";
                if (workingSlice.StartsWith(MEDIA_INFO_KEY))
                {
                    currentExtMediaInfo = Stream.ExtMediaInfo.Parse(workingSlice);
                }
                else if (workingSlice.StartsWith(STREAM_INFO_KEY))
                {
                    currentExtStreamInfo = Stream.ExtStreamInfo.Parse(workingSlice);
                }
                else if (workingSlice.StartsWith(PROGRAM_DATE_TIME_KEY))
                {
                    currentExtProgramDateTime = ParsingHelpers.ParseDateTimeOffset(workingSlice, PROGRAM_DATE_TIME_KEY);
                }
                else if (workingSlice.StartsWith(BYTE_RANGE_KEY))
                {
                    currentByteRange = Stream.ExtByteRange.Parse(workingSlice);
                }
                else if (workingSlice.StartsWith(PART_INFO_KEY))
                {
                    currentExtPartInfo = Stream.ExtPartInfo.Parse(workingSlice);
                }
                else if (workingSlice.StartsWith(END_LIST_KEY))
                {
                    break;
                }
                else
                {
                    metadataBuilder.ParseAndAppend(workingSlice);
                }

                if (lineEnd == -1)
                    break;

            } while ((textStart += lineEnd) < textEnd);

            return new M3U8(metadataBuilder.ToMetadata(), streams.ToArray());
        }

        public sealed record Metadata
        {
            public enum PlaylistType
            {
                Unknown,
                Vod,
                Event
            }

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
                    _metadata ??= new Metadata();

                    const string TARGET_VERSION_KEY = "#EXT-X-VERSION:";
                    const string TARGET_DURATION_KEY = "#EXT-X-TARGETDURATION:";
                    const string PLAYLIST_TYPE_KEY = "#EXT-X-PLAYLIST-TYPE:";
                    const string MEDIA_SEQUENCE_KEY = "#EXT-X-MEDIA-SEQUENCE:";
                    const string TWITCH_LIVE_SEQUENCE_KEY = "#EXT-X-TWITCH-LIVE-SEQUENCE:";
                    const string TWITCH_ELAPSED_SECS_KEY = "#EXT-X-TWITCH-ELAPSED-SECS:";
                    const string TWITCH_TOTAL_SECS_KEY = "#EXT-X-TWITCH-TOTAL-SECS:";
                    const string TWITCH_INFO_KEY = "#EXT-X-TWITCH-INFO:";
                    if (text.StartsWith(TARGET_VERSION_KEY))
                    {
                        _metadata.Version = ParsingHelpers.ParseUIntValue(text, TARGET_VERSION_KEY);
                    }
                    else if (text.StartsWith(TARGET_DURATION_KEY))
                    {
                        _metadata.StreamTargetDuration = ParsingHelpers.ParseUIntValue(text, TARGET_DURATION_KEY);
                    }
                    else if (text.StartsWith(PLAYLIST_TYPE_KEY))
                    {
                        var temp = text[PLAYLIST_TYPE_KEY.Length..];
                        if (temp.StartsWith("VOD"))
                            _metadata.Type = PlaylistType.Vod;
                        else if (temp.StartsWith("EVENT"))
                            _metadata.Type = PlaylistType.Event;
                        else
                            throw new FormatException($"Unable to parse PlaylistType from: {text}");
                    }
                    else if (text.StartsWith(MEDIA_SEQUENCE_KEY))
                    {
                        _metadata.MediaSequence = ParsingHelpers.ParseUIntValue(text, MEDIA_SEQUENCE_KEY);
                    }
                    else if (text.StartsWith(TWITCH_LIVE_SEQUENCE_KEY))
                    {
                        _metadata.TwitchLiveSequence = ParsingHelpers.ParseUIntValue(text, TWITCH_LIVE_SEQUENCE_KEY);
                    }
                    else if (text.StartsWith(TWITCH_ELAPSED_SECS_KEY))
                    {
                        _metadata.TwitchElapsedSeconds = ParsingHelpers.ParseDecimalValue(text, TWITCH_ELAPSED_SECS_KEY);
                    }
                    else if (text.StartsWith(TWITCH_TOTAL_SECS_KEY))
                    {
                        _metadata.TwitchTotalSeconds = ParsingHelpers.ParseDecimalValue(text, TWITCH_TOTAL_SECS_KEY);
                    }
                    else if (text.StartsWith(TWITCH_INFO_KEY))
                    {
                        // Do nothing. This header includes response related info that we don't need.
                    }
                    else if (text[0] == '#')
                    {
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
                    sb.AppendLine(Path);

                if (sb.Length == 0)
                    return "";

                sb.Append("#EXT-X-ENDLIST");
                return sb.ToString();
            }

            public readonly record struct ExtByteRange(uint Start, uint Length)
            {
                public static implicit operator ExtByteRange((uint start, uint length) tuple) => new(tuple.start, tuple.length);
                public override string ToString() => $"#EXT-X-BYTERANGE:{Start}@{Length}";

                public static ExtByteRange Parse(ReadOnlySpan<char> text)
                {
                    if (text.StartsWith("#EXT-X-BYTERANGE:"))
                        text = text[17..];

                    var separatorIndex = text.IndexOf('@');
                    if (separatorIndex == -1)
                        throw new FormatException($"Unable to parse ByteRange from {text}.");

                    if (!uint.TryParse(text[..separatorIndex], out var start))
                        throw new FormatException($"Unable to parse ByteRange from {text}.");

                    if (!uint.TryParse(text[(separatorIndex + 1)..], out var end))
                        throw new FormatException($"Unable to parse ByteRange from {text}.");

                    return new ExtByteRange(start, end);
                }
            }

            public sealed class ExtMediaInfo
            {
                public enum MediaType
                {
                    Unknown,
                    Video,
                    Audio
                }

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
                    static string BooleanToWord(bool b)
                    {
                        return b ? "YES" : "NO";
                    }

                    return $"#EXT-X-MEDIA:TYPE={Type.ToString().ToUpper()},GROUP-ID=\"{GroupId}\",NAME=\"{Name}\",AUTOSELECT={BooleanToWord(AutoSelect)},DEFAULT={BooleanToWord(Default)}";
                }

                public static ExtMediaInfo Parse(ReadOnlySpan<char> text)
                {
                    var mediaInfo = new ExtMediaInfo();

                    if (text.StartsWith("#EXT-X-MEDIA:"))
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
                            if (temp.StartsWith("VIDEO"))
                                mediaInfo.Type = MediaType.Video;
                            else if (temp.StartsWith("AUDIO"))
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
                    public static implicit operator StreamResolution((uint width, uint height) tuple) => new(tuple.width, tuple.height);

                    public override string ToString() => $"{Width}x{Height}";

                    public static StreamResolution Parse(ReadOnlySpan<char> text)
                    {
                        if (text.StartsWith("RESOLUTION="))
                            text = text[11..];

                        var separatorIndex = text.IndexOfAny("x");
                        if (separatorIndex == -1 || separatorIndex == text.Length)
                            throw new FormatException($"Unable to parse Resolution from {text}.");

                        if (!uint.TryParse(text[..separatorIndex], out var width))
                            throw new FormatException($"Unable to parse Resolution from {text}.");

                        if (!uint.TryParse(text[(separatorIndex + 1)..], out var height))
                            throw new FormatException($"Unable to parse Resolution from {text}.");

                        return new StreamResolution(width, height);
                    }
                }

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

                public override string ToString() => $"#EXT-X-STREAM-INF:PROGRAM-ID={ProgramId},BANDWIDTH={Bandwidth},CODECS=\"{Codecs}\",RESOLUTION={Resolution},VIDEO=\"{Video}\",FRAME-RATE={Framerate}";

                public static ExtStreamInfo Parse(ReadOnlySpan<char> text)
                {
                    var streamInfo = new ExtStreamInfo();

                    if (text.StartsWith("#EXT-X-STREAM-INF:"))
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
                            streamInfo.ProgramId = ParsingHelpers.ParseIntValue(text, KEY_PROGRAM_ID);
                        }
                        else if (text.StartsWith(KEY_BANDWIDTH))
                        {
                            streamInfo.Bandwidth = ParsingHelpers.ParseIntValue(text, KEY_BANDWIDTH);
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
                            streamInfo.Framerate = ParsingHelpers.ParseDecimalValue(text, KEY_FRAMERATE);
                        }

                        var nextIndex = text.UnEscapedIndexOf(',');
                        if (nextIndex == -1)
                            break;

                        text = text[(nextIndex + 1)..];
                    } while (true);

                    return streamInfo;
                }
            }

            public sealed record ExtPartInfo
            {
                private ExtPartInfo() { }

                public ExtPartInfo(decimal duration, bool live)
                {
                    Duration = duration;
                    Live = live;
                }

                public decimal Duration { get; private set; }
                public bool Live { get; private set; }

                public override string ToString() => $"#EXTINF:{Duration},{(Live ? "live" : "")}";

                public static ExtPartInfo Parse(ReadOnlySpan<char> text)
                {
                    var partInfo = new ExtPartInfo();

                    if (text.StartsWith("#EXTINF:"))
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
            public static bool TryParseM3UHeader(ref ReadOnlySpan<char> text)
            {
                const string M3U_HEADER = "#EXTM3U";
                if (!text.StartsWith(M3U_HEADER))
                {
                    return false;
                }

                text = text[7..].TrimStart(" \r\n");
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

            public static int ParseIntValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (int.TryParse(temp, out var intValue))
                    return intValue;

                throw new FormatException($"Unable to parse integer from: {text}");
            }

            public static uint ParseUIntValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (uint.TryParse(temp, out var uIntValue))
                    return uIntValue;

                throw new FormatException($"Unable to parse integer from: {text}");
            }

            public static decimal ParseDecimalValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (decimal.TryParse(temp, out var decimalValue))
                    return decimalValue;

                throw new FormatException($"Unable to parse decimal from: {text}");
            }

            public static bool ParseBooleanValue(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];

                if (temp.StartsWith("NO"))
                    return false;

                if (temp.StartsWith("YES"))
                    return true;

                temp = temp[..NextKeyStart(temp)];

                if (bool.TryParse(temp, out var booleanValue))
                    return booleanValue;

                throw new FormatException($"Unable to parse boolean from: {text}");
            }

            public static Stream.ExtStreamInfo.StreamResolution ParseResolution(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                return Stream.ExtStreamInfo.StreamResolution.Parse(temp);
            }

            public static DateTimeOffset ParseDateTimeOffset(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                var temp = text[keyName.Length..];
                temp = temp[..NextKeyStart(temp)];

                if (DateTimeOffset.TryParse(temp, out var dateTimeOffset))
                    return dateTimeOffset;

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
    }
}