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

namespace TwitchDownloaderCLI.Modes
{
    internal static class UpdateHandler
    {
        private static HttpClient _client = new();

        public static void ParseArgs(UpdateArgs args)
        {
#if DEBUG
            Console.WriteLine("Auto-update is not supported for debug builds");
#else
            CheckForUpdate(args.ForceUpdate).GetAwaiter().GetResult();
#endif
        }

        private static async Task CheckForUpdate(bool forceUpdate)
        {
            // Get the old version
            Version oldVersion = Assembly.GetExecutingAssembly().GetName().Version!.StripRevisionIfDefault();

            // Get the new version
            var updateInfoUrl = @"https://downloader-update.twitcharchives.workers.dev/";
            using HttpResponseMessage response = await _client.GetAsync(updateInfoUrl);
            string xmlString = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(xmlString))
            {
                Console.Error.WriteLine("Internal error: could not parse remote update info XML!");
            }

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlString);

            string newVersionString = xmlDoc.DocumentElement.SelectSingleNode("/item/version").InnerText;

            Version newVersion = new Version(newVersionString);

            if (newVersion <= oldVersion)
            {
                Console.WriteLine("You have the latest version of TwitchDownloader CLI!");
                return;
            }

            Console.WriteLine($"A new version of TwitchDownloader CLI is available ({newVersionString})!");

            string origUrl = xmlDoc.DocumentElement.SelectSingleNode("/item/url-cli").InnerText;
            string urlBase = new Regex(@"(.*)\/").Match(origUrl).Groups[1].Value;

            string origPackageName = origUrl.Split("/").Last();
            string packageNameBase = new Regex(@"(.*)-\{0\}").Match(origPackageName).Groups[1].Value;

            // Construct the appropriate package name
            string packageName = ConstructPackageName(packageNameBase);

            if (packageName == string.Empty)
            {
                throw new PlatformNotSupportedException("Current OS and architecture not supported for auto-update");
            }

            string newUrl = urlBase + "/" + packageName;

            if (forceUpdate)
            {
                await AutoUpdate(newUrl);
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
                            await AutoUpdate(newUrl);
                            return;
                        case "n" or "no":
                            return;
                    }
                }
            }
        }

        private static string ConstructPackageName(string packageNameBase)
        {
            var arch = RuntimeInformation.OSArchitecture;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return packageNameBase + "-Windows-x64.zip";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return arch switch
                {
                    Architecture.X64 => packageNameBase + "-MacOS-x64.zip",
                    Architecture.Arm64 => packageNameBase + "-MacOSArm64.zip",
                    _ => string.Empty
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // TODO: Change to 'linux-musl-x64' when .NET 8+
                if (RuntimeInformation.RuntimeIdentifier.Contains("musl"))
                {
                    return packageNameBase + "-LinuxAlpine-x64.zip";
                } 
                else
                {
                    return arch switch
                    {
                        Architecture.X64 => packageNameBase + "-Linux-x64.zip",
                        Architecture.Arm => packageNameBase + "-LinuxArm.zip",
                        Architecture.Arm64 => packageNameBase + "-LinuxArm64.zip",
                        _ => string.Empty
                    };
                }    
            }

            return string.Empty;

        }

        private static async Task AutoUpdate(string url)
        {
            var currentExePath = Environment.ProcessPath;
            var oldExePath = currentExePath + ".bak";
            var updateDir = Path.GetDirectoryName(currentExePath);
            var archivePath = Path.Combine(updateDir, url.Split("/").Last());

            if (string.IsNullOrEmpty(currentExePath))
            {
                Console.Error.WriteLine("Internal error: Current executable path is null or empty!");
            }

            if (string.IsNullOrEmpty(updateDir))
            {
                Console.Error.WriteLine("Internal error: Update directory is null or empty!");
            }

            Console.WriteLine("Downloading update archive...");
            Stream s = await _client.GetStreamAsync(url);

            // Create downloaded archive file from stream data
            using (var fs = new FileStream(archivePath, FileMode.OpenOrCreate))
            {
                s.CopyTo(fs);
            }

            // Check for a previous update
            if (File.Exists(oldExePath))
            {
                File.Delete(oldExePath);
            }

            // Rename current exe
            File.Move(currentExePath, oldExePath);

            if (File.Exists(Path.Combine(updateDir, "COPYRIGHT.txt")))
            {
                File.Delete(Path.Combine(updateDir, "COPYRIGHT.txt"));
            }

            if (File.Exists(Path.Combine(updateDir, "THIRD-PARTY-LICENSES.txt")))
            {
                File.Delete(Path.Combine(updateDir, "THIRD-PARTY-LICENSES.txt"));
            }

            ZipFile.ExtractToDirectory(archivePath, updateDir);

            // Clean up downloaded archive
            File.Delete(archivePath);

            Console.WriteLine("TwitchDownloader CLI has been updated!");
        }
    }
}
