using System;
using System.Diagnostics;
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
using TwitchDownloaderCLI.Models;
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
            using var progress = new CliTaskProgress(args.LogLevel);
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
            var xmlString = await HttpClient.GetStringAsync("https://downloader-update.twitcharchives.workers.dev/").ConfigureAwait(false);
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
                progress.LogInfo($"You have the latest version of {nameof(TwitchDownloaderCLI)}");
                return;
            }

            progress.LogInfo($"{nameof(TwitchDownloaderCLI)} v{newVersion} is available!");

            var origUrl = xmlDoc.DocumentElement!.SelectSingleNode("/item/url-cli")!.InnerText;
            var urlBase = Regex.Match(origUrl, @"(.*)\/").Groups[1].Value;

            // Construct the appropriate package name
            var packageName = ConstructPackageName(origUrl.Split('/').Last());
            if (string.IsNullOrWhiteSpace(packageName))
            {
                progress.LogVerbose($"Could not construct package name for arch: {RuntimeInformation.OSArchitecture}, RID: {RuntimeInformation.RuntimeIdentifier}");
                throw new PlatformNotSupportedException("Self-update is not supported for the current OS/architecture.");
            }

            var newUrl = $"{urlBase}/{packageName}";
            progress.LogVerbose($"Constructed download URL: {newUrl}");

            if (args.ForceUpdate)
            {
                await AutoUpdate(newUrl, args.KeepArchive, newVersion, progress).ConfigureAwait(false);
                return;
            }

            var promptResult = UserPrompt.ShowYesNo("Would you like to update?", progress);
            if (promptResult is UserPromptResult.Yes)
            {
                await AutoUpdate(newUrl, args.KeepArchive, newVersion, progress).ConfigureAwait(false);
            }
        }

        [return: MaybeNull]
        private static string ConstructPackageName(string origPackageName)
        {
            var arch = RuntimeInformation.OSArchitecture;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return arch switch
                {
                    Architecture.X64 => string.Format(origPackageName, "Windows-x64"),
                    _ => null
                };
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
                if (RuntimeInformation.RuntimeIdentifier.Contains("musl"))
                {
                    return arch switch
                    {
                        Architecture.X64 => string.Format(origPackageName, "LinuxAlpine-x64"),
                        _ => null
                    };
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

        private static async Task AutoUpdate(string url, bool keepArchive, Version newVersion, ITaskProgress progress)
        {
            var currentExePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExePath))
            {
                progress.LogError("Cannot update: process path was null or empty! (Was the executable moved or deleted?)");
                return;
            }

            progress.SetTemplateStatus("Downloading Update {0}%", 0);

            var updateDir = Path.GetDirectoryName(currentExePath)!;
            var archivePath = Path.Combine(updateDir, url.Split('/').Last());
            await DownloadUpdateArchive(url, progress, archivePath).ConfigureAwait(false);

            // Get old unix file permissions
            var previousPermissions = GetUnixFilePermissions(progress, currentExePath);

            // Create backup
            BackupCurrentExecutable(currentExePath, progress);

            progress.SetTemplateStatus("Extracting Files {0}%", 0);

            await ExtractZipFiles(progress, archivePath, updateDir).ConfigureAwait(false);

            if (!keepArchive)
            {
                File.Delete(archivePath);
            }

            ApplyUnixFilePermissions(progress, currentExePath, previousPermissions);

            progress.LogInfo($"{nameof(TwitchDownloaderCLI)} has been updated to v{newVersion}!");
        }

        private static async Task DownloadUpdateArchive(string url, ITaskProgress progress, string archivePath)
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var archiveLength = response.Content.Headers.ContentLength;

            // Create downloaded archive file from stream data
            await using (var fs = new FileStream(archivePath, FileMode.OpenOrCreate))
            {
                await contentStream.ProgressCopyToAsync(fs, archiveLength, new Progress<StreamCopyProgress>(DownloadProgressHandler)).ConfigureAwait(false);
            }

            progress.ReportProgress(100);

            void DownloadProgressHandler(StreamCopyProgress streamProgress)
            {
                var percent = (int)(streamProgress.BytesCopied / (double)streamProgress.SourceLength * 100);
                progress.ReportProgress(percent);
            }
        }

        private static UnixFileMode? GetUnixFilePermissions(ITaskProgress progress, string currentExePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    return new FileInfo(currentExePath).UnixFileMode;
                }
                catch (Exception ex)
                {
                    progress.LogError($"Failed to get current file permissions for {Path.GetFileName(currentExePath)}: {ex.Message}");
                }
            }

            return null;
        }

        private static void ApplyUnixFilePermissions(ITaskProgress progress, string currentExePath, UnixFileMode? previousPermissions)
        {
            if (OperatingSystem.IsWindows() || !previousPermissions.HasValue)
            {
                return;
            }

            try
            {
                var fi = new FileInfo(currentExePath);
                fi.UnixFileMode = previousPermissions.Value;
                fi.Refresh();
            }
            catch (Exception ex)
            {
                var processFilename = Path.GetFileName(currentExePath);
                var chmodCommand = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "chmod +x" : "sudo chmod +x";
                progress.LogError($"Unable to restore previous file permissions: {ex.Message} Please run '{chmodCommand} {processFilename}' to allow {processFilename} to be executed.");
            }
        }

        private static void BackupCurrentExecutable(string currentExePath, ITaskProgress progress)
        {
            try
            {
                var backupPath = currentExePath + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(currentExePath, backupPath);

                progress.LogVerbose($"Successfully created backup of current version at {backupPath}");
            }
            catch (Exception ex)
            {
                progress.LogWarning($"Failed to create backup: {ex.Message}");
            }
        }

        private static async Task ExtractZipFiles(ITaskProgress progress, string archivePath, string destinationDir)
        {
            await using (var archiveFs = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var archive = new ZipArchive(archiveFs, ZipArchiveMode.Read);

                var entryCount = archive.Entries.Count;
                var extracted = 0;

                foreach (var entry in archive.Entries)
                {
                    entry.ExtractToFile(Path.Combine(destinationDir, entry.FullName), true);
                    extracted++;

                    var percent = (int)(extracted / (double)entryCount * 100);
                    progress.ReportProgress(percent);
                }
            }

            progress.ReportProgress(100);
        }
    }
}