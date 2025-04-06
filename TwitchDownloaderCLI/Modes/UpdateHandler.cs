using CommandLine.Text;
using System;
using System.Net.Http;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;
using System.IO.Compression;
using TwitchDownloaderCLI.Modes.Arguments;
using System.Reflection;
using TwitchDownloaderCore.Extensions;
using System.Runtime.InteropServices;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCLI.Tools;
using Spectre.Console;
using System.Diagnostics;

namespace TwitchDownloaderCLI.Modes
{
    internal static class UpdateHandler
    {
        private static readonly HttpClient _client = new();

        public static void ParseArgs(UpdateArgs args)
        {
            var progress = new CliTaskProgress(args.LogLevel);
#if DEBUG
            progress.LogInfo("Auto-update is not supported for debug builds");
#else
            CheckForUpdate(args.ForceUpdate, progress).GetAwaiter().GetResult();
#endif
        }

        private static async Task CheckForUpdate(bool forceUpdate, CliTaskProgress progress)
        {
            // Get the old version
            Version oldVersion = Assembly.GetExecutingAssembly().GetName().Version!.StripRevisionIfDefault();

            // Get the new version
            var updateInfoUrl = @"https://downloader-update.twitcharchives.workers.dev/";
            var xmlString = await _client.GetStringAsync(updateInfoUrl);

            if (string.IsNullOrEmpty(xmlString))
            {
                progress.LogError("Could not parse remote update info XML!");
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);

            string newVersionString = xmlDoc.DocumentElement!.SelectSingleNode("/item/version").InnerText;

            Version newVersion = new Version(newVersionString);

            if (newVersion <= oldVersion)
            {
                progress.LogInfo("You have the latest version of TwitchDownloader CLI!");
                return;
            }

            progress.LogInfo($"A new version of TwitchDownloader CLI is available ({newVersionString})!");

            string origUrl = xmlDoc.DocumentElement.SelectSingleNode("/item/url-cli").InnerText;
            string urlBase = new Regex(@"(.*)\/").Match(origUrl).Groups[1].Value;

            // Construct the appropriate package name
            string packageName = ConstructPackageName(origUrl.Split("/").Last());

            if (packageName == string.Empty)
            {
                throw new PlatformNotSupportedException("Current OS and architecture not supported for auto-update");
            }

            string newUrl = urlBase + "/" + packageName;

            if (forceUpdate)
            {
                await AutoUpdate(newUrl, progress);
            }
            else
            {
                Console.WriteLine("Would you like to auto-update?");

                while (true)
                {
                    Console.WriteLine("[Y] Yes / [N] No: ");
                    var userInput = Console.ReadLine()!.Trim().ToLower();

                    switch (userInput)
                    {
                        case "y" or "yes":
                            await AutoUpdate(newUrl, progress);
                            return;
                        case "n" or "no":
                            return;
                    }
                }
            }
        }

        private static string ConstructPackageName(string origPackageName)
        {
            var arch = RuntimeInformation.OSArchitecture;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return string.Format(origPackageName, "Windows-x64");
                
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return arch switch
                {
                    Architecture.X64 => string.Format(origPackageName, "MacOS-x64"),
                    Architecture.Arm64 => string.Format(origPackageName, "MacOSArm64"),
                    _ => string.Empty
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // TODO: Change to 'linux-musl-x64' when .NET 8+
                if (RuntimeInformation.RuntimeIdentifier.Contains("musl"))
                {
                    return string.Format(origPackageName, "LinuxAlpine-x64");
                } 
                else
                {
                    return arch switch
                    {
                        Architecture.X64 => string.Format(origPackageName, "Linux-x64"),
                        Architecture.Arm => string.Format(origPackageName, "LinuxArm"),
                        Architecture.Arm64 => string.Format(origPackageName, "LinuxArm64"),
                        _ => string.Empty
                    };
                }    
            }

            return string.Empty;
        }

        private static async Task AutoUpdate(string url, CliTaskProgress progress)
        {
            var currentExePath = Environment.ProcessPath;
            var oldExePath = currentExePath + ".bak";
            var updateDir = Path.GetDirectoryName(currentExePath)!;
            var archivePath = Path.Combine(updateDir, url.Split("/").Last());

            if (string.IsNullOrEmpty(currentExePath))
            {
                progress.LogError("Current executable path is null or empty!");
            }

            if (string.IsNullOrEmpty(updateDir))
            {
                progress.LogError("Update directory is null or empty!");
            }

            progress.LogInfo("Downloading update archive...");

            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var contentStream = await response.Content.ReadAsStreamAsync();

            var archiveLength = response.Content.Headers.ContentLength;

            // Create downloaded archive file from stream data
            await using (var fs = new FileStream(archivePath, FileMode.OpenOrCreate))
            {
                await contentStream.ProgressCopyToAsync(fs, archiveLength, new Progress<StreamCopyProgress>(DownloadProgressHandler)).ConfigureAwait(false);
            }

            // Check for a previous update
            if (File.Exists(oldExePath))
            {
                File.Delete(oldExePath);
            }

            // Rename current exe
            File.Move(currentExePath!, oldExePath);

            await using (var archiveFs = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var archive = new ZipArchive(archiveFs, ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    entry.ExtractToFile(Path.Combine(updateDir, entry.FullName), true);
                }
            }

            progress.LogInfo("TwitchDownloader CLI has been updated!");

            void DownloadProgressHandler(StreamCopyProgress streamProgress)
            {
                var percent = (int)(streamProgress.BytesCopied / (double)streamProgress.SourceLength * 100);
                progress.ReportProgress(percent);
            }
        }

    }
}
