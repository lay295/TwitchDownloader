using System;

namespace TwitchDownloaderCore.Extensions
{
    public static class VersionExtensions
    {
        public static Version StripRevisionIfDefault(this Version version) => version.Revision < 1 ? new Version(version.Major, version.Minor, version.Build) : version;
    }
}