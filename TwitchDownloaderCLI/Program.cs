using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CommandLine.Text;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI
{
    class Program
    {
        private static void Main(string[] args)
        {
            // Set the working dir to the app dir in case we inherited a different working dir
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            var preParsedArgs = PreParseArgs.Parse(args, Path.GetFileName(Environment.ProcessPath));

            var parser = new Parser(config =>
            {
                config.CaseInsensitiveEnumValues = true;
                config.HelpWriter = TextWriter.Null;
            });

            var parserResult = parser.ParseArguments<VideoDownloadArgs, ClipDownloadArgs, ChatDownloadArgs, ChatUpdateArgs, ChatRenderArgs, FfmpegArgs, CacheArgs>(preParsedArgs);
            parserResult.WithNotParsed(errors => WriteHelpText(errors, parserResult, parser.Settings));

            CoreLicensor.EnsureFilesExist(AppContext.BaseDirectory);
            WriteApplicationBanner((ITwitchDownloaderArgs)parserResult.Value, args);

            parserResult
                .WithParsed<VideoDownloadArgs>(DownloadVideo.Download)
                .WithParsed<ClipDownloadArgs>(DownloadClip.Download)
                .WithParsed<ChatDownloadArgs>(DownloadChat.Download)
                .WithParsed<ChatUpdateArgs>(UpdateChat.Update)
                .WithParsed<ChatRenderArgs>(RenderChat.Render)
                .WithParsed<FfmpegArgs>(FfmpegHandler.ParseArgs)
                .WithParsed<CacheArgs>(CacheHandler.ParseArgs);
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
                        builder.Copyright = GetManifestInfo().LegalCopyright!.Replace("\u00A9", "(c)");
                        return builder;
                    }));
            }

            Environment.Exit(1);
        }

        private static void WriteApplicationBanner(ITwitchDownloaderArgs argsBase, string[] args)
        {
            if (argsBase.ShowBanner == false || args.Contains("--silent"))
            {
                return;
            }

            var manifestInfo = GetManifestInfo();
            Console.WriteLine($"{manifestInfo.ProductName} v{manifestInfo.ProductVersion} {manifestInfo.LegalCopyright!.Replace("\u00A9", "(c)")}");
        }

        private static FileVersionInfo GetManifestInfo()
        {
            var assemblyFileName = Path.GetFileName(Environment.ProcessPath)!;
            return FileVersionInfo.GetVersionInfo(assemblyFileName);
        }
    }
}