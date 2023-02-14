using System;
using System.Text.RegularExpressions;
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
            if (inputOptions.Id is null)
            {
                Console.WriteLine("[ERROR] - Clip ID/URL cannot be null!");
                Environment.Exit(1);
            }

            var clipIdRegex = new Regex(@"(?<=(?:clips\.)?twitch\.tv\/(?:\S+\/clip\/)?)\D\w+(?:-\S+)?(?=$|\?)");
            var clipIdMatch = clipIdRegex.Match(inputOptions.Id);
            if (!clipIdMatch.Success)
            {
                Console.WriteLine("[ERROR] - Unable to parse Clip ID/URL.");
                Environment.Exit(1);
            }

            ClipDownloadOptions downloadOptions = new()
            {
                Id = clipIdMatch.Value,
                Filename = inputOptions.OutputFile,
                Quality = inputOptions.Quality
            };

            return downloadOptions;
        }
    }
}
