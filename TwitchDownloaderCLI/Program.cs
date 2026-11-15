using CommandLine;
using CommandLine.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI
{
    internal static partial class Program
    {
        internal static void Main(string[] args)
        {
            var preParsedArgs = PreParseArgs.Parse(args, Path.GetFileName(Environment.ProcessPath));

            var parserResult = ParseVerbs(preParsedArgs, out var parserSettings);
            parserResult.WithNotParsed(errors => WriteHelpText(errors, parserResult, parserSettings));

            CoreLicensor.EnsureFilesExist(null);
            WriteApplicationBanner((ITwitchDownloaderArgs)parserResult.Value);

            parserResult
                .WithParsed<VideoDownloadArgs>(DownloadVideo.Download)
                .WithParsed<ClipDownloadArgs>(DownloadClip.Download)
                .WithParsed<ChatDownloadArgs>(DownloadChat.Download)
                .WithParsed<ChatUpdateArgs>(UpdateChat.Update)
                .WithParsed<ChatRenderArgs>(RenderChat.Render)
                .WithParsed<InfoArgs>(InfoHandler.PrintInfo)
                .WithParsed<FfmpegArgs>(FfmpegHandler.ParseArgs)
                .WithParsed<CacheArgs>(CacheHandler.ParseArgs)
                .WithParsed<UpdateArgs>(UpdateHandler.ParseArgs)
                .WithParsed<TsMergeArgs>(MergeTs.Merge);
        }

        /// <summary>Parses the (pre-parsed) arguments into one of the verb argument types. The tests parse through this as well.</summary>
        internal static ParserResult<object> ParseVerbs(string[] preParsedArgs, out ParserSettings settings)
        {
            var parser = new Parser(config =>
            {
                config.CaseInsensitiveEnumValues = true;
                config.HelpWriter = null; // Use null instead of TextWriter.Null due to how CommandLine works internally
            });

            settings = parser.Settings;
            return parser.ParseArguments<VideoDownloadArgs, ClipDownloadArgs, ChatDownloadArgs, ChatUpdateArgs, ChatRenderArgs, InfoArgs, FfmpegArgs, CacheArgs, UpdateArgs, TsMergeArgs>(preParsedArgs);
        }

        private static void WriteHelpText(IEnumerable<Error> errors, ParserResult<object> parserResult, ParserSettings parserSettings)
        {
            if (errors.FirstOrDefault()?.Tag == ErrorType.NoVerbSelectedError)
            {
                var processFileName = Path.GetFileName(Environment.ProcessPath);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Some Windows users try to double click the executable
                    Console.WriteLine("This is a command line tool. Please open a terminal and run \"{0} help\" from there for more information.{1}Press any key to close...",
                        processFileName, Environment.NewLine);
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine("Usage: {0} [VERB] [OPTIONS]{1}Try \'{2} help\' for more information.",
                        processFileName, Environment.NewLine, processFileName);
                }
            }
            else
            {
                Console.Error.WriteLine(
                    HelpText.AutoBuild(parserResult, builder =>
                    {
                        builder.MaximumDisplayWidth = parserSettings.MaximumDisplayWidth;
                        builder.Copyright = CopyrightInfo.Default.ToString()!.Replace("\u00A9", "(c)");
                        return builder;
                    }));
            }

            Environment.Exit(1);
        }

        [GeneratedRegex("""(?<=\d)\+[0-9a-f]+""")]
        internal static partial Regex GitHashRegex { get; }

        internal static void WriteApplicationBanner(ITwitchDownloaderArgs args)
        {
            if (args.ShowBanner == false || (args.LogLevel & LogLevel.None) != 0)
            {
                return;
            }

            var nameVersionString = HeadingInfo.Default.ToString();

#if !DEBUG
            // Remove git commit hash from version string
            nameVersionString = GitHashRegex.Replace(nameVersionString, "");
#endif

            Console.WriteLine($"{nameVersionString} {CopyrightInfo.Default.ToString()!.Replace("\u00A9", "(c)")}");
        }
    }
}