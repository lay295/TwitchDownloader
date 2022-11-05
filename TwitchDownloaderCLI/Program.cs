using CommandLine;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI
{
    class Program
    {
        private static readonly string ffmpegExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";

        static void Main(string[] args)
        {
            if (args.Any(x => x.Equals("--download-ffmpeg")))
            {
                FfmpegHandler.DownloadFfmpeg();
            }

            if (args.Any(x => x.Equals("--clear-cache")))
            {
                ClearCache.PromptClearCache();
            }

            Options inputOptions = new();
            var optionsResult = Parser.Default.ParseArguments<Options>(args).WithParsed(r => { inputOptions = r; });
            if (optionsResult.Tag == ParserResultType.NotParsed)
            {
                Environment.Exit(1);
            }

            FfmpegHandler.DetectFfmpeg(ffmpegExecutableName, inputOptions.FfmpegPath, inputOptions.RunMode);

            switch (inputOptions.RunMode)
            {
                case RunMode.VideoDownload:
                    DownloadVideo.Download(inputOptions, ffmpegExecutableName);
                    break;
                case RunMode.ClipDownload:
                    DownloadClip.Download(inputOptions);
                    break;
                case RunMode.ChatDownload:
                    DownloadChat.Download(inputOptions);
                    break;
                case RunMode.ChatRender:
                    RenderChat.Render(inputOptions, ffmpegExecutableName);
                    break;
            }
        }
    }
}