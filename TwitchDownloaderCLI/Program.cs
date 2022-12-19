using CommandLine;
using System;
using System.IO;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            string processFileName = Path.GetFileName(Environment.ProcessPath);
            
            WriteNoArgHelpText(args, processFileName);

            string[] preParsedArgs = PreParseArgs.Parse(args, processFileName);

            Parser.Default.ParseArguments<VideoDownloadArgs, ClipDownloadArgs, ChatDownloadArgs, ChatUpdateArgs, ChatRenderArgs, FfmpegArgs, CacheArgs>(preParsedArgs)
                .WithParsed<VideoDownloadArgs>(DownloadVideo.Download)
                .WithParsed<ClipDownloadArgs>(DownloadClip.Download)
                .WithParsed<ChatDownloadArgs>(DownloadChat.Download)
                .WithParsed<ChatUpdateArgs>(UpdateChat.Update)
                .WithParsed<ChatRenderArgs>(RenderChat.Render)
                .WithParsed<FfmpegArgs>(FfmpegHandler.ParseArgs)
                .WithParsed<CacheArgs>(CacheHandler.ParseArgs)
                .WithNotParsed(_ => Environment.Exit(1));
        }

        static void WriteNoArgHelpText(string[] args, string processFileName)
        {
            if (args.Length == 0)
            {
                if (Path.GetExtension(processFileName).Equals(".exe"))
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
                Environment.Exit(1);
            }
        }
    }
}