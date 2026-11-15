using System.Diagnostics;
using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    /// <summary>
    /// The unique, solid colours of the images embedded in <c>TestData/Render/synthetic_chat.json</c>. None of them can be produced by
    /// anti-aliased text (white/grey on the dark background), so finding one exactly in a frame proves that image was drawn.
    /// </summary>
    internal static class FixtureColors
    {
        public static readonly SKColor FirstPartyEmote = new(0xFFFF00FF); // "TestEmote", emoticon_id 1001, 56x56
        public static readonly SKColor StaticThirdParty = new(0xFF00FFFF); // "StaticThird", 56x56
        public static readonly SKColor[] AnimatedFrames = [new(0xFFFF0000), new(0xFF00FF00), new(0xFF0000FF)]; // "AnimThird" GIF, 100/200/300 ms
        public static readonly SKColor ZeroWidth = new(0xFFFF8000); // "ZeroWidth", 16x16 opaque centre in a transparent 56x56
        public static readonly SKColor SubscriberBadge = new(0xFFFFFF00);
        public static readonly SKColor ModeratorBadge = new(0xFF008000);
        public static readonly SKColor BroadcasterBadge = new(0xFF800000);
        public static readonly SKColor BitsBadge = new(0xFF808000);
        public static readonly SKColor Cheer1 = new(0xFF804000);
        public static readonly SKColor Cheer100 = new(0xFF004080);
        public static readonly SKColor Purple = new(0xFF7B2CF2); // ChatRenderer's highlight accent colour
    }

    /// <summary>
    /// Runs <see cref="ChatRenderer"/> end to end, offline, against <c>TestData/Render/synthetic_chat.json</c> (3 comments, 3 seconds, with embedded
    /// badges/emotes/cheermotes) using a real ffmpeg that writes raw RGBA frames, so tests can assert on exact pixels.
    /// Defaults mirror the CLI defaults except for a small canvas, 10 fps and a 0.1 s update rate to keep each render well under a second.
    /// </summary>
    internal sealed class RenderHarness : IDisposable
    {
        public const string RAW_OUTPUT_ARGS = "-f rawvideo -pix_fmt rgba \"{save_path}\"";
        public static readonly SKColor Background = new(0xFF111111);
        public static readonly DateTime VideoCreated = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public TempDirectory Temp { get; } = new();
        public RecordingTaskProgress Progress { get; } = new();
        public ChatRenderOptions Options { get; }

        public RenderHarness()
        {
            Options = new ChatRenderOptions
            {
                InputFile = TestData.Path("Render", "synthetic_chat.json"),
                OutputFile = Temp.Combine("chat.rgba"),
                BackgroundColor = Background,
                AlternateBackgroundColor = new SKColor(0xFF191919),
                HighlightUserColor = new SKColor(0xC8FF0059),
                MessageColor = SKColors.White,
                ChatWidth = 300,
                ChatHeight = 200,
                BttvEmotes = true,
                FfzEmotes = true,
                StvEmotes = true,
                OutlineSize = 4,
                Font = "Inter Embedded",
                FontSize = 12,
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                Framerate = 10,
                UpdateRate = 0.1,
                InputArgs = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -",
                OutputArgs = RAW_OUTPUT_ARGS,
                FfmpegPath = TestData.RequireFfmpeg(),
                TempFolder = Temp.Path,
                SubMessages = true,
                ChatBadges = true,
                Offline = true,
                EmojiVendor = EmojiVendor.None,
                AdjustUsernameVisibility = true,
                DisperseCommentOffsets = false,
            };
        }

        /// <summary>Parses the fixture, lets the test edit the parsed chat, renders it and reads back the raw RGBA output.</summary>
        public async Task<RenderedVideo> RenderAsync(Action<ChatRoot>? editChat = null, CancellationToken cancellationToken = default)
        {
            await RenderOnlyAsync(editChat, cancellationToken);
            return RenderedVideo.Read(Options.OutputFile, Options.ChatWidth, Options.ChatHeight, 4);
        }

        /// <summary>Like <see cref="RenderAsync"/> but leaves reading the output (which may not be raw RGBA) to the test.</summary>
        public async Task RenderOnlyAsync(Action<ChatRoot>? editChat = null, CancellationToken cancellationToken = default)
        {
            using var renderer = new ChatRenderer(Options, Progress);
            var chat = await renderer.ParseJsonAsync(cancellationToken);
            editChat?.Invoke(chat);
            await renderer.RenderVideoAsync(cancellationToken);
        }

        /// <summary>Builds a plain chat comment. Every commenter gets a grey name so usernames can never be mistaken for a fixture image.</summary>
        public static Comment MakeComment(double offset, string login, string body, List<Fragment>? fragments = null, List<UserBadge>? badges = null,
            int bits = 0, string? displayName = null, string? msgId = null, string? logo = null, string? userId = null)
        {
            return new Comment
            {
                _id = Guid.NewGuid().ToString(),
                created_at = VideoCreated.AddSeconds(offset),
                channel_id = "1000",
                content_type = "video",
                content_id = "2000",
                content_offset_seconds = offset,
                commenter = new Commenter { name = login, display_name = displayName ?? login, _id = userId ?? login.GetHashCode().ToString(), logo = logo },
                message = new Message
                {
                    body = body,
                    bits_spent = bits,
                    fragments = fragments ?? [new Fragment { text = body }],
                    user_badges = badges ?? [],
                    user_color = "#C0C0C0",
                    user_notice_params = msgId is null ? null : new UserNoticeParams { msg_id = msgId },
                    emoticons = [],
                }
            };
        }

        /// <summary>A short message containing the first-party fixture emote, so the comment's presence can be detected by colour.</summary>
        public static Comment EmoteComment(double offset, string login = "alice", string? displayName = null, List<UserBadge>? badges = null) =>
            MakeComment(offset, login, "TestEmote", [new Fragment { text = "TestEmote", emoticon = new Emoticon { emoticon_id = "1001" } }], badges, displayName: displayName);

        public void Dispose() => Temp.Dispose();

        /// <summary>Runs ffmpeg and returns its exit code and stderr, for probing render output (ffprobe is not guaranteed to be installed).</summary>
        public static (int ExitCode, string StdErr) RunFfmpeg(params string[] args)
        {
            var psi = new ProcessStartInfo(TestData.RequireFfmpeg()) { RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)!;
            var stderr = process.StandardError.ReadToEndAsync();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stderr.Result);
        }
    }

    /// <summary>Raw frames written by ffmpeg (<c>-f rawvideo</c>), either RGBA (4 bytes/pixel) or a gray mask (1 byte/pixel).</summary>
    internal sealed class RenderedVideo(byte[] data, int width, int height, int bytesPerPixel)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int FrameSize => Width * Height * bytesPerPixel;
        public int FrameCount => data.Length / FrameSize;
        public bool IsWholeFrames => data.Length % FrameSize == 0;

        public static RenderedVideo Read(string path, int width, int height, int bytesPerPixel) => new(File.ReadAllBytes(path), width, height, bytesPerPixel);

        public SKColor Pixel(int frame, int x, int y)
        {
            var i = frame * FrameSize + (y * Width + x) * bytesPerPixel;
            return bytesPerPixel == 1
                ? new SKColor(data[i], data[i], data[i])
                : new SKColor(data[i], data[i + 1], data[i + 2], data[i + 3]);
        }

        public int Count(int frame, Func<SKColor, bool> predicate, int yStart = 0, int yEnd = -1)
        {
            if (yEnd < 0) yEnd = Height;
            var count = 0;
            for (var y = yStart; y < yEnd; y++)
            for (var x = 0; x < Width; x++)
            {
                if (predicate(Pixel(frame, x, y))) count++;
            }

            return count;
        }

        public int Count(int frame, SKColor color) => Count(frame, c => c == color);

        /// <summary>How many columns contain <paramref name="color"/>, i.e. the drawn width of a solid image.</summary>
        public int ColumnCount(int frame, SKColor color) =>
            Enumerable.Range(0, Width).Count(x => Enumerable.Range(0, Height).Any(y => Pixel(frame, x, y) == color));

        /// <summary>The leftmost column containing <paramref name="color"/>, or -1.</summary>
        public int FirstColumnOf(int frame, SKColor color)
        {
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
            {
                if (Pixel(frame, x, y) == color) return x;
            }

            return -1;
        }

        /// <summary>Every position in the frame whose pixel is exactly <paramref name="color"/>, row by row.</summary>
        public IEnumerable<(int X, int Y)> PixelsOf(int frame, SKColor color)
        {
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (Pixel(frame, x, y) == color) yield return (x, y);
            }
        }

        /// <summary>
        /// The horizontal bands (runs of consecutive rows) containing <paramref name="color"/>, top to bottom, with the leftmost column
        /// of the colour in each band. Each band is one chat line the colour appears on, so tests need not know the layout's row height.
        /// </summary>
        public List<(int Top, int Bottom, int Left)> Bands(int frame, SKColor color)
        {
            var bands = new List<(int Top, int Bottom, int Left)>();
            foreach (var (x, y) in PixelsOf(frame, color))
            {
                if (bands.Count > 0 && y - bands[^1].Bottom <= 1)
                    bands[^1] = (bands[^1].Top, y, Math.Min(bands[^1].Left, x));
                else
                    bands.Add((y, y, x));
            }

            return bands;
        }

        /// <summary>The topmost row that differs from <paramref name="background"/>, or -1 if the frame is empty.</summary>
        public int FirstNonBackgroundRow(int frame, SKColor background)
        {
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (Pixel(frame, x, y) != background) return y;
            }

            return -1;
        }
    }
}
