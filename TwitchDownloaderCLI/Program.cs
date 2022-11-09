using CommandLine;
using System;
using System.IO;
using System.Linq;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                string processName = Environment.ProcessPath.Split(Path.DirectorySeparatorChar).Last();
                if (Path.GetExtension(processName).Equals(".exe"))
                {
                    // Windows users are far more likely to double click the executable like a normal program
                    Console.WriteLine("This is a command line tool. Please open a terminal and run \"{0} --help\" from there for more information.{1}Press any key to close...",
                        processName, Environment.NewLine);
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine("USAGE: {0} [VERB] [OPTIONS]{1}Try \'{2} --help\' for more information.",
                        processName, Environment.NewLine, processName);
                }
                Environment.Exit(1);
            }

            string[] preParsedArgs;
            if (args.Any(x => x.Equals("-m") || x.Equals("--mode")))
            {
                Console.WriteLine("[INFO] The program has switched from --mode <mode> to verbs (like \"git <verb>\"), consider using verbs instead. Run \"TwitchDownloaderCLI help\" for more info.");
                preParsedArgs = PreParseArgs.Process(PreParseArgs.ConvertFromOldSyntax(args));
            }
            else
            {
                preParsedArgs = PreParseArgs.Process(args);
            }

            Parser.Default.ParseArguments<VideoDownloadArgs, ClipDownloadArgs, ChatDownloadArgs, ChatRenderArgs, FfmpegArgs, CacheArgs>(preParsedArgs)
                .WithParsed<VideoDownloadArgs>(DownloadVideo.Download)
                .WithParsed<ClipDownloadArgs>(DownloadClip.Download)
                .WithParsed<ChatDownloadArgs>(DownloadChat.Download)
                .WithParsed<ChatRenderArgs>(RenderChat.Render)
                .WithParsed<FfmpegArgs>(FfmpegHandler.ParseArgs)
                .WithParsed<CacheArgs>(CacheHandler.ParseArgs)
                .WithNotParsed(_ => Environment.Exit(1));
        }
    }
}