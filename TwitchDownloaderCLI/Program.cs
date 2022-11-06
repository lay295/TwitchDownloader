using CommandLine;
using System;
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
                .WithParsed<VideoDownloadArgs>(r => DownloadVideo.Download(r))
                .WithParsed<ClipDownloadArgs>(r => DownloadClip.Download(r))
                .WithParsed<ChatDownloadArgs>(r => DownloadChat.Download(r))
                .WithParsed<ChatRenderArgs>(r => RenderChat.Render(r))
                .WithParsed<FfmpegArgs>(r => FfmpegHandler.ParseArgs(r))
                .WithParsed<CacheArgs>(r => CacheHandler.ParseArgs(r))
                .WithNotParsed(_ => Environment.Exit(1));
        }
    }
}