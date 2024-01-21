using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.Extensions.Hosting.Internal;
using Mono.Unix;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCLI.Modes.Arguments;

namespace TwitchDownloaderCLI.Tools
{
    public class CurlHandler
    {
        public static readonly string[] FilesToExtract = new string[] { "curl-ca-bundle.crt", "libcurl.dll", "libcurl.a", "libcurldll.a",  };

        public static void DetectCurl(string curlImpersonatePath)
        {
            throw new NotImplementedException();

            Console.WriteLine("[ERROR] - Unable to find curl-impersonate, exiting.");
            Environment.Exit(1);
        }
        public static void ParseArgs(CurlArgs args)
        {
            if (args.DownloadCurl)
            {
                DownloadCurl(args).Wait();
            }
        }

        private static async Task DownloadCurl(CurlArgs args)
        {
            using HttpClient httpClient = new HttpClient();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (HttpResponseMessage response = await httpClient.GetAsync("https://github.com/depler/curl-impersonate-win/releases/download/20230227/curl-impersonate-win.zip"))
                using (Stream streamToReadFrom = await response.Content.ReadAsStreamAsync())
                {
                    using (var fs = new FileStream("curl-impersonate-win.zip", FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                using (var archive = ZipFile.OpenRead("curl-impersonate-win.zip"))
                {
                    foreach (var item in archive.Entries)
                    {
                        if (FilesToExtract.Contains(item.Name))
                        {
                            item.ExtractToFile(Path.Combine(Directory.GetCurrentDirectory(), item.FullName.Replace("curl-impersonate-win/", "")), true);
                        }
                    }
                }

                File.Delete("curl-impersonate-win.zip");
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
