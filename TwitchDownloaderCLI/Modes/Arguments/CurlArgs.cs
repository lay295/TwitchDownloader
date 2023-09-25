using CommandLine.Text;
using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("curl", HelpText = "Manage standalone curl-impersonate")]
    public class CurlArgs : ITwitchDownloaderArgs
    {
        [Option('d', "download", Default = false, Required = false, HelpText = "Downloads curl-impersonate as a standalone file.")]
        public bool DownloadCurl { get; set; }

        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }
    }
}
