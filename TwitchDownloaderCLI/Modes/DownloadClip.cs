using System;
using System.Linq;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class DownloadClip
    {
        internal static void Download(Options inputOptions)
        {
            if (inputOptions.Id == string.Empty || inputOptions.Id.All(char.IsDigit))
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

            ClipDownloader clipDownloader = new(downloadOptions);
            clipDownloader.DownloadAsync().Wait();
        }
    }
}
