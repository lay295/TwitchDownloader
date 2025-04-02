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

namespace TwitchDownloaderCLI.Modes
{
    internal static class UpdateHandler
    {
        private static HttpClient _client = new();

        public static void ParseArgs(UpdateArgs args)
        {
#if !DEBUG
            CheckForUpdate(args.ForceUpdate).GetAwaiter().GetResult();
#endif
        }

        private static async Task CheckForUpdate(bool forceUpdate)
        {
            // Get the old version
            string headerString = HeadingInfo.Default.ToString();
            Regex versionPattern = new Regex(@"([0-9]+\.[0-9]+\.[0-9]+)");
            Match m = versionPattern.Match(headerString);
            string oldVersionString = m.Success ? m.Groups[1].Value : string.Empty;

            if (oldVersionString == string.Empty)
            {
                Console.Error.WriteLine("Internal error: could not parse old version string!");
                return;
            }

            Version oldVersion = new Version(oldVersionString);

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

            if (newVersion.CompareTo(oldVersion) > 0)
            {
                Console.WriteLine($"A new version of TwitchDownloader CLI is available ({newVersionString})!");

                // We want the download for the CLI version, not the GUI version
                string oldUrl = xmlDoc.DocumentElement.SelectSingleNode("/item/url").InnerText;
                string newUrl = Regex.Replace(oldUrl, "GUI", "CLI");

                if (forceUpdate)
                {
                    await AutoUpdate(newUrl);
                }
                else
                {
                    Console.WriteLine("Would you like to auto-update? (y/n):");
                    var input = Console.ReadLine();

                    if (input == "y")
                    {
                        await AutoUpdate(newUrl);
                    }
                }
            }
            else if (newVersion.CompareTo(oldVersion) == 0)
            {
                Console.WriteLine("You have the latest version of TwitchDownloader CLI!");
            }
        }

        private static async Task AutoUpdate(string url)
        {
            Console.WriteLine("Downloading update archive...");
            Stream s = await _client.GetStreamAsync(url);
            var archiveName = url.Split("/").Last();

            // Create downloaded archive file from stream data
            using (var fs = new FileStream(archiveName, FileMode.OpenOrCreate))
            {
                s.CopyTo(fs);
            }

            var exeName = Path.GetFileName(Environment.ProcessPath);
            var oldExe = exeName + ".bak";

            if (string.IsNullOrEmpty(exeName))
            {
                Console.Error.WriteLine("Internal error: Executable name is null!");
            }

            // Check for a previous update
            if (File.Exists(oldExe))
            {
                File.Delete(oldExe);
            }

            // Rename current exe
            File.Move(exeName, oldExe);

            if (File.Exists("COPYRIGHT.txt"))
            {
                File.Delete("COPYRIGHT.txt");
            }

            if (File.Exists("THIRD-PARTY-LICENSES.txt"))
            {
                File.Delete("THIRD-PARTY-LICENSES.txt");
            }

            ZipFile.ExtractToDirectory(archiveName, ".");

            // Clean up downloaded archive
            File.Delete(archiveName);

            Console.WriteLine("TwitchDownloader CLI has been updated!");
        }
    }
}
