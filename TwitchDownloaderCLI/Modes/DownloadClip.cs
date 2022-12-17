using System;
using System.Linq;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class DownloadClip
    {
        internal static void Download(ClipDownloadArgs inputOptions)
        {
            ClipDownloadOptions downloadOptions = GetDownloadOptions(inputOptions);

            ClipDownloader clipDownloader = new(downloadOptions);
            clipDownloader.DownloadAsync().Wait();
        }

        private static ClipDownloadOptions GetDownloadOptions(ClipDownloadArgs inputOptions)
        {
            if (string.IsNullOrWhiteSpace(inputOptions.Id) || inputOptions.Id.All(char.IsDigit))
            {
                Console.WriteLine("[ERROR] - Invalid Clip ID, unable to parse.");
                Environment.Exit(1);
            }

            ClipDownloadOptions downloadOptions = new()
            {
                Id = inputOptions.Id,
                Filename = inputOptions.OutputFile,
                Quality = inputOptions.Quality
            };

            return downloadOptions;
        }
    }
}
