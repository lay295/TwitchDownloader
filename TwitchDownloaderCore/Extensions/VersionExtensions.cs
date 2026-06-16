namespace TwitchDownloaderCore.Extensions
{
    public static class VersionExtensions
    {
        extension(Version version)
        {
            public Version StripRevisionIfDefault() => version.Revision < 1 ? new Version(version.Major, version.Minor, version.Build) : version;
        }
    }
}