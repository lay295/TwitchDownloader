using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Models
{
    public partial record M3U8
    {
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
            ByteRange currentByteRange = default;
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
            ByteRange currentByteRange = default;
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
            out ByteRange currentByteRange, out Stream.ExtPartInfo currentExtPartInfo)
        {
            currentExtMediaInfo = null;
            currentExtStreamInfo = null;
            currentExtProgramDateTime = default;
            currentByteRange = default;
            currentExtPartInfo = null;
        }

        private static bool ParseM3U8Key(ReadOnlySpan<char> text, Metadata.Builder metadataBuilder, ref Stream.ExtMediaInfo extMediaInfo, ref Stream.ExtStreamInfo extStreamInfo,
            ref DateTimeOffset extProgramDateTime, ref ByteRange byteRange, ref Stream.ExtPartInfo extPartInfo)
        {
            const string END_LIST_KEY = "#EXT-X-ENDLIST";
            if (text.StartsWith(Stream.ExtMediaInfo.MEDIA_INFO_KEY))
            {
                extMediaInfo = Stream.ExtMediaInfo.Parse(text);
            }
            else if (text.StartsWith(Stream.ExtStreamInfo.STREAM_INFO_KEY))
            {
                extStreamInfo = Stream.ExtStreamInfo.Parse(text);
            }
            else if (text.StartsWith(Stream.PROGRAM_DATE_TIME_KEY))
            {
                extProgramDateTime = ParsingHelpers.ParseDateTimeOffset(text, Stream.PROGRAM_DATE_TIME_KEY, false);
            }
            else if (text.StartsWith(Stream.BYTE_RANGE_KEY))
            {
                byteRange = ByteRange.Parse(text, Stream.BYTE_RANGE_KEY);
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

        public partial record Metadata
        {
            public sealed class Builder
            {
                // Generic M3U headers
                private uint? _version;
                private uint? _streamTargetDuration;
                private PlaylistType? _type;
                private uint? _mediaSequence;
                private ExtMap _map;

                // Twitch specific
                private uint? _twitchLiveSequence;
                private decimal? _twitchElapsedSeconds;
                private decimal? _twitchTotalSeconds;

                // Other headers that we don't have dedicated properties for. Useful for debugging.
                private readonly List<KeyValuePair<string, string>> _unparsedValues = new();

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
                        _version = ParsingHelpers.ParseUIntValue(text, TARGET_VERSION_KEY);
                    }
                    else if (text.StartsWith(TARGET_DURATION_KEY))
                    {
                        _streamTargetDuration = ParsingHelpers.ParseUIntValue(text, TARGET_DURATION_KEY);
                    }
                    else if (text.StartsWith(PLAYLIST_TYPE_KEY))
                    {
                        var temp = text[PLAYLIST_TYPE_KEY.Length..];
                        if (temp.StartsWith(PLAYLIST_TYPE_VOD))
                            _type = PlaylistType.Vod;
                        else if (temp.StartsWith(PLAYLIST_TYPE_EVENT))
                            _type = PlaylistType.Event;
                        else
                            throw new FormatException($"Unable to parse PlaylistType from: {text}");
                    }
                    else if (text.StartsWith(MEDIA_SEQUENCE_KEY))
                    {
                        _mediaSequence = ParsingHelpers.ParseUIntValue(text, MEDIA_SEQUENCE_KEY);
                    }
                    else if (text.StartsWith(MAP_KEY))
                    {
                        _map = ExtMap.Parse(text);
                    }
                    else if (text.StartsWith(TWITCH_LIVE_SEQUENCE_KEY))
                    {
                        _twitchLiveSequence = ParsingHelpers.ParseUIntValue(text, TWITCH_LIVE_SEQUENCE_KEY);
                    }
                    else if (text.StartsWith(TWITCH_ELAPSED_SECS_KEY))
                    {
                        _twitchElapsedSeconds = ParsingHelpers.ParseDecimalValue(text, TWITCH_ELAPSED_SECS_KEY);
                    }
                    else if (text.StartsWith(TWITCH_TOTAL_SECS_KEY))
                    {
                        _twitchTotalSeconds = ParsingHelpers.ParseDecimalValue(text, TWITCH_TOTAL_SECS_KEY);
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
                            _unparsedValues.Add(kvp);
                        }
                        else
                        {
                            var kvp = new KeyValuePair<string, string>("", text.ToString());
                            _unparsedValues.Add(kvp);
                        }
                    }
                }

                public Metadata ToMetadata()
                {
                    return new Metadata
                    {
                        Version = _version,
                        StreamTargetDuration = _streamTargetDuration,
                        Type = _type,
                        MediaSequence = _mediaSequence,
                        Map = _map,
                        TwitchLiveSequence = _twitchLiveSequence,
                        TwitchElapsedSeconds = _twitchElapsedSeconds,
                        TwitchTotalSeconds = _twitchTotalSeconds,
                        _unparsedValues = _unparsedValues
                    };
                }
            }

            public partial record ExtMap
            {
                public static ExtMap Parse(ReadOnlySpan<char> text)
                {
                    if (text.StartsWith(MAP_KEY))
                        text = text[MAP_KEY.Length..];

                    ByteRange byteRange = default;
                    var uri = "";

                    do
                    {
                        text = text.TrimStart();

                        if (text.StartsWith(BYTE_RANGE_KEY))
                        {
                            byteRange = ByteRange.Parse(text, BYTE_RANGE_KEY);
                        }
                        else if (text.StartsWith(URI_KEY))
                        {
                            uri = ParsingHelpers.ParseStringValue(text, URI_KEY);
                        }

                        var nextIndex = text.UnEscapedIndexOf(',');
                        if (nextIndex == -1)
                            break;

                        text = text[(nextIndex + 1)..];
                    } while (true);

                    return new ExtMap(uri, byteRange);
                }
            }
        }

        public partial record Stream
        {
            public partial record ExtMediaInfo
            {
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

            public partial record ExtStreamInfo
            {
                public partial record struct StreamResolution
                {
                    public static StreamResolution Parse(ReadOnlySpan<char> text)
                    {
                        if (text.StartsWith("RESOLUTION="))
                            text = text[11..];

                        var separatorIndex = text.IndexOfAny("x");
                        if (separatorIndex != -1
                            && separatorIndex != text.Length
                            && uint.TryParse(text[..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
                            && uint.TryParse(text[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
                        {
                            return new StreamResolution(width, height);
                        }

                        throw new FormatException($"Unable to parse Resolution from {text}.");
                    }
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
                            var codecsString = ParsingHelpers.ParseStringValue(text, KEY_CODECS);
                            streamInfo.Codecs = codecsString.Split(',');
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

            public partial record ExtPartInfo
            {
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

        public partial record struct ByteRange
        {
            public static ByteRange Parse(ReadOnlySpan<char> text, ReadOnlySpan<char> keyName)
            {
                if (text.StartsWith(keyName))
                    text = text[keyName.Length..];

                var separatorIndex = text.IndexOf('@');
                if (separatorIndex != -1
                    && separatorIndex != text.Length
                    && uint.TryParse(text[..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                    && uint.TryParse(text[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
                {
                    return new ByteRange(start, end);
                }

                throw new FormatException($"Unable to parse ByteRange from {text}.");
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
    }
}