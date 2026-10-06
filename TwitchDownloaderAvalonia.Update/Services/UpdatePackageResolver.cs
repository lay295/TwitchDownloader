using System.Runtime.InteropServices;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.Update.Services
{
    /// <summary>
    /// Builds the Avalonia download URL from the workers feed or a GitHub fallback.
    /// Release/CI must publish <c>TwitchDownloaderAvalonia-{ver}-{platform}.zip</c> and may add
    /// <c>url-avalonia</c> to the workers XML when ready. Never use feed <c>url</c> (WPF) or <c>url-cli</c>.
    /// </summary>
    public static class UpdatePackageResolver
    {
        public const string GIT_HUB_DOWNLOAD_TEMPLATE = "https://github.com/lay295/TwitchDownloader/releases/download/{0}/TwitchDownloaderAvalonia-{0}-{1}.zip";

        public static string? TryGetPlatformToken(UpdateHostOs os, Architecture architecture, string runtimeIdentifier)
        {
            if (os == UpdateHostOs.Windows)
                return architecture == Architecture.X64 ? "Windows-x64" : null;

            if (os == UpdateHostOs.MacOs)
            {
                return architecture switch
                {
                    Architecture.X64 => "MacOS-x64",
                    Architecture.Arm64 => "MacOSArm64",
                    _ => null,
                };
            }

            if (os == UpdateHostOs.Linux)
            {
                if (runtimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase))
                    return architecture == Architecture.X64 ? "LinuxAlpine-x64" : null;

                return architecture switch
                {
                    Architecture.X64 => "Linux-x64",
                    Architecture.Arm => "LinuxArm",
                    Architecture.Arm64 => "LinuxArm64",
                    _ => null,
                };
            }

            return null;
        }

        public static string? TryConstructPackageName(
            string origPackageName,
            UpdateHostOs os,
            Architecture architecture,
            string runtimeIdentifier)
        {
            var token = TryGetPlatformToken(os, architecture, runtimeIdentifier);
            if (token is null || string.IsNullOrWhiteSpace(origPackageName))
                return null;

            try
            {
                return string.Format(origPackageName, token);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public static string? TryResolveDownloadUrl(
            string? avaloniaUrlTemplate,
            Version version,
            UpdateHostOs os,
            Architecture architecture,
            string runtimeIdentifier)
        {
            var token = TryGetPlatformToken(os, architecture, runtimeIdentifier);
            if (token is null)
                return null;

            if (!string.IsNullOrWhiteSpace(avaloniaUrlTemplate))
                return TryResolveFromTemplate(avaloniaUrlTemplate, os, architecture, runtimeIdentifier);

            return string.Format(GIT_HUB_DOWNLOAD_TEMPLATE, version.ToString(), token);
        }

        public static string? TryResolveDownloadUrlForCurrentOs(string? avaloniaUrlTemplate, Version version)
        {
            return TryResolveDownloadUrl(
                avaloniaUrlTemplate,
                version,
                UpdateHost.CurrentOs,
                UpdateHost.CurrentArchitecture,
                UpdateHost.CurrentRuntimeIdentifier);
        }

        private static string? TryResolveFromTemplate(
            string template,
            UpdateHostOs os,
            Architecture architecture,
            string runtimeIdentifier)
        {
            var lastSlash = template.LastIndexOf('/');
            if (lastSlash <= 0 || lastSlash >= template.Length - 1)
                return null;

            var urlBase = template[..lastSlash];
            var origPackageName = template.AsSpan().GetNthOccurrence('/', ^1).ToString();
            var packageName = TryConstructPackageName(origPackageName, os, architecture, runtimeIdentifier);
            if (string.IsNullOrWhiteSpace(packageName))
                return null;

            return $"{urlBase}/{packageName}";
        }
    }
}
