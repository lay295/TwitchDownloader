using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Mono.Unix;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCLI.Modes
{
    internal static class UpdateHandler
    {
        private static readonly HttpClient HttpClient = new();

        public static void ParseArgs(UpdateArgs args)
        {
            var progress = new CliTaskProgress(args.LogLevel);
#if DEBUG
            progress.LogInfo("Self-update is not supported for debug builds");
#else
            CheckForUpdate(args, progress).GetAwaiter().GetResult();
#endif
        }

        private static async Task CheckForUpdate(UpdateArgs args, ITaskProgress progress)
        {
            // Get the current version
            var oldVersion = Assembly.GetExecutingAssembly().GetName().Version!.StripRevisionIfDefault();

            // Get the new version
            var xmlString = await HttpClient.GetStringAsync("https://downloader-update.twitcharchives.workers.dev/");
            if (string.IsNullOrWhiteSpace(xmlString))
            {
                progress.LogError("Could not parse remote update info XML");
                return;
            }

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);

            var newVersionString = xmlDoc.DocumentElement!.SelectSingleNode("/item/version")!.InnerText;
            var newVersion = new Version(newVersionString);

            if (newVersion <= oldVersion)
            {
                progress.LogInfo("You have the latest version of TwitchDownloaderCLI");
                return;
            }

            progress.LogInfo($"A new version of TwitchDownloaderCLI is available (v{newVersionString})!");

            var origUrl = xmlDoc.DocumentElement!.SelectSingleNode("/item/url-cli")!.InnerText;
            var urlBase = Regex.Match(origUrl, @"(.*)\/").Groups[1].Value;

            // Construct the appropriate package name
            var packageName = ConstructPackageName(origUrl.Split('/').Last());

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new PlatformNotSupportedException("Self-update is not supported for the current OS/architecture");
            }

            var newUrl = $"{urlBase}/{packageName}";
            if (args.ForceUpdate)
            {
                await AutoUpdate(newUrl, progress);
            }
            else
            {
                Console.WriteLine("Would you like to update?");

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

            progress.LogInfo($"TwitchDownloaderCLI has been updated to v{newVersion}!");
        }

        [return: MaybeNull]
        private static string ConstructPackageName(string origPackageName)
        {
            var arch = RuntimeInformation.OSArchitecture;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return string.Format(origPackageName, "Windows-x64");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return arch switch
                {
                    Architecture.X64 => string.Format(origPackageName, "MacOS-x64"),
                    Architecture.Arm64 => string.Format(origPackageName, "MacOSArm64"),
                    _ => null
                };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // TODO: Change to 'linux-musl-x64' when .NET 8+
                if (RuntimeInformation.RuntimeIdentifier.Contains("musl"))
                {
                    return string.Format(origPackageName, "LinuxAlpine-x64");
                }

                return arch switch
                {
                    Architecture.X64 => string.Format(origPackageName, "Linux-x64"),
                    Architecture.Arm => string.Format(origPackageName, "LinuxArm"),
                    Architecture.Arm64 => string.Format(origPackageName, "LinuxArm64"),
                    _ => null
                };
            }

            return null;
        }

        private static async Task AutoUpdate(string url, ITaskProgress progress)
        {
            var currentExePath = Environment.ProcessPath;
            var oldExePath = currentExePath + ".bak";
            var updateDir = Path.GetDirectoryName(currentExePath)!;
            var archivePath = Path.Combine(updateDir, url.Split('/').Last());

            if (string.IsNullOrEmpty(currentExePath))
            {
                progress.LogError("Current executable path is null or empty!");
                return;
            }

            if (string.IsNullOrEmpty(updateDir))
            {
                progress.LogError("Update directory is null or empty!");
                return;
            }

            progress.SetTemplateStatus("Downloading Update {0}%", 0);

            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            var archiveLength = response.Content.Headers.ContentLength;

            // Create downloaded archive file from stream data
            await using (var fs = new FileStream(archivePath, FileMode.OpenOrCreate))
            {
                await contentStream.ProgressCopyToAsync(fs, archiveLength, new Progress<StreamCopyProgress>(DownloadProgressHandler)).ConfigureAwait(false);
            }

            progress.ReportProgress(100);

            // Check for a previous update
            if (File.Exists(oldExePath))
            {
                File.Delete(oldExePath);
            }

            // Get old unix file permissions
            FileAccessPermissions? previousPermissions = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    previousPermissions = new UnixFileInfo(currentExePath).FileAccessPermissions;
                }
                catch (Exception ex)
                {
                    progress.LogError($"Failed to get current file permissions for {Path.GetFileName(currentExePath)}: {ex.Message}");
                }
            }

            // Rename current exe
            File.Move(currentExePath!, oldExePath);

            progress.SetTemplateStatus("Extracting Files {0}%", 0);

            await using (var archiveFs = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var archive = new ZipArchive(archiveFs, ZipArchiveMode.Read);

                var entryCount = archive.Entries.Count;
                var extracted = 0;

                foreach (var entry in archive.Entries)
                {
                    entry.ExtractToFile(Path.Combine(updateDir, entry.FullName), true);
                    extracted++;

                    var percent = (int)(extracted / (double)entryCount * 100);
                    progress.ReportProgress(percent);
                }
            }

            progress.ReportProgress(100);

            // Clean up downloaded archive
            File.Delete(archivePath);

            // Apply previous file permissions
            if (previousPermissions.HasValue)
            {
                try
                {
                    var ufi = new UnixFileInfo(currentExePath)
                    {
                        FileAccessPermissions = previousPermissions.Value
                    };
                    ufi.Refresh();
                }
                catch (Exception ex)
                {
                    var processFilename = Path.GetFileName(currentExePath);
                    var chmodCommand = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "chmod +x" : "sudo chmod +x";
                    progress.LogError($"Unable to restore previous file permissions: {ex.Message} Please run '{chmodCommand} {processFilename}' to allow {processFilename} to be executed.");
                }
            }

            void DownloadProgressHandler(StreamCopyProgress streamProgress)
            {
                var percent = (int)(streamProgress.BytesCopied / (double)streamProgress.SourceLength * 100);
                progress.ReportProgress(percent);
            }
        }
    }
}