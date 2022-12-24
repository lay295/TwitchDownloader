using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("chatrender", HelpText = "Renders a chat JSON as a video")]
    public class ChatRenderArgs
    {
        [Option('i', "input", Required = true, HelpText = "Path to JSON chat file input.")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "File the program will output to.")]
        public string OutputFile { get; set; }

        [Option("background-color", Default = "#111111", HelpText = "Color of background in HEX string format.")]
        public string BackgroundColor { get; set; }

        [Option("message-color", Default = "#ffffff", HelpText = "Color of messages in HEX string format.")]
        public string MessageColor { get; set; }

        [Option('w', "chat-width", Default = 350, HelpText = "Width of chat render.")]
        public int ChatWidth { get; set; }

        [Option('h', "chat-height", Default = 600, HelpText = "Height of chat render.")]
        public int ChatHeight { get; set; }

        [Option("bttv", Default = true, HelpText = "Enable BTTV emotes.")]
        public bool? BttvEmotes { get; set; }

        [Option("ffz", Default = true, HelpText = "Enable FFZ emotes.")]
        public bool? FfzEmotes { get; set; }

        [Option("stv", Default = true, HelpText = "Enable 7tv emotes.")]
        public bool? StvEmotes { get; set; }

        [Option("sub-messages", Default = true, HelpText = "Enable sub/re-sub messages.")]
        public bool? SubMessages { get; set; }

        [Option("badges", Default = true, HelpText = "Enable chat badges.")]
        public bool? ChatBadges { get; set; }

        [Option("outline", Default = false, HelpText = "Enable outline around chat messages.")]
        public bool Outline { get; set; }

        [Option("outline-size", Default = 4, HelpText = "Size of outline if outline is enabled.")]
        public double OutlineSize { get; set; }

        [Option('f', "font", Default = "Inter Embedded", HelpText = "Font to use.")]
        public string Font { get; set; }

        [Option("font-size", Default = 12, HelpText = "Font size.")]
        public double FontSize { get; set; }

        [Option("message-fontstyle", Default = "normal", HelpText = "Font style of messages. Valid values are normal, bold, and italic.")]
        public string MessageFontStyle { get; set; }

        [Option("username-fontstyle", Default = "bold", HelpText = "Font style of usernames. Valid values are normal, bold, and italic.")]
        public string UsernameFontStyle { get; set; }

        [Option("timestamp", Default = false, HelpText = "Enable timestamps to the left of messages, similar to VOD chat on Twitch.")]
        public bool Timestamp { get; set; }

        [Option("generate-mask", Default = false, HelpText = "Generates a mask file of the chat in addition to the rendered chat.")]
        public bool GenerateMask { get; set; }

        [Option("framerate", Default = 30, HelpText = "Framerate of the render.")]
        public int Framerate { get; set; }

        [Option("update-rate", Default = 0.2, HelpText = "Time in seconds to update chat render output.")]
        public double UpdateRate { get; set; }

        [Option("input-args", Default = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", HelpText = "Input arguments for ffmpeg chat render.")]
        public string InputArgs { get; set; }

        [Option("output-args", Default = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"", HelpText = "Output arguments for ffmpeg chat render.")]
        public string OutputArgs { get; set; }

        [Option("ignore-users", Default = "", HelpText = "List of usernames to ignore when rendering, separated by commas. Not case-sensitive.")]
        public string IgnoreUsersString { get; set; }

        [Option("ban-words", Default = "", HelpText = "List of words or phrases to ignore when rendering, separated by commas. Not case-sensitive.")]
        public string BannedWordsString { get; set; }

        [Option("badge-filter", Default = 0, HelpText = "Bitmask of types of Chat Badges to filter out. Add the numbers of the types of badges you want to filter. For example, 6 = no broadcaster or moderator badges.\r\nKey: Other = 1, Broadcaster = 2, Moderator = 4, VIP = 8, Subscriber = 16, Predictions = 32, NoAudio/NoVideo = 64, PrimeGaming = 128")]
        public int BadgeFilterMask { get; set; }

        [Option("offline", Default = false, HelpText = "Render completely offline using only embedded emotes, badges, and bits from the input json.")]
        public bool Offline { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to ffmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary folder to use for cache.")]
        public string TempFolder { get; set; }

        [Option("verbose-ffmpeg", Default = false, HelpText = "Prints every message from ffmpeg.")]
        public bool LogFfmpegOutput { get; set; }
    }
}