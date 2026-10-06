using System.Runtime.InteropServices;
using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class UpdatePackageResolverTests
    {
        [Theory]
        [InlineData(UpdateHostOs.Windows, Architecture.X64, "win-x64", "Windows-x64")]
        [InlineData(UpdateHostOs.MacOs, Architecture.Arm64, "osx-arm64", "MacOSArm64")]
        [InlineData(UpdateHostOs.MacOs, Architecture.X64, "osx-x64", "MacOS-x64")]
        [InlineData(UpdateHostOs.Linux, Architecture.X64, "linux-x64", "Linux-x64")]
        [InlineData(UpdateHostOs.Linux, Architecture.X64, "linux-musl-x64", "LinuxAlpine-x64")]
        [InlineData(UpdateHostOs.Linux, Architecture.Arm64, "linux-arm64", "LinuxArm64")]
        [InlineData(UpdateHostOs.Linux, Architecture.Arm, "linux-arm", "LinuxArm")]
        public void ResolvesKnownPlatformTokens(UpdateHostOs os, Architecture arch, string rid, string expected)
        {
            Assert.Equal(expected, UpdatePackageResolver.TryGetPlatformToken(os, arch, rid));
        }

        [Theory]
        [InlineData(UpdateHostOs.Windows, Architecture.Arm64, "win-arm64")]
        [InlineData(UpdateHostOs.Other, Architecture.X64, "browser-wasm")]
        [InlineData(UpdateHostOs.Linux, Architecture.Arm64, "linux-musl-arm64")]
        public void UnsupportedPlatformReturnsNull(UpdateHostOs os, Architecture arch, string rid)
        {
            Assert.Null(UpdatePackageResolver.TryGetPlatformToken(os, arch, rid));
            Assert.Null(UpdatePackageResolver.TryResolveDownloadUrl(null, new Version(1, 56, 5), os, arch, rid));
        }

        [Fact]
        public void FeedTemplateWinsOverGitHubFallback()
        {
            var url = UpdatePackageResolver.TryResolveDownloadUrl(
                "https://cdn.example/TwitchDownloaderAvalonia-9.9.9-{0}.zip",
                new Version(9, 9, 9),
                UpdateHostOs.Windows,
                Architecture.X64,
                "win-x64");

            Assert.Equal("https://cdn.example/TwitchDownloaderAvalonia-9.9.9-Windows-x64.zip", url);
        }

        [Fact]
        public void MissingTemplateUsesGitHubFallback()
        {
            var url = UpdatePackageResolver.TryResolveDownloadUrl(
                null,
                new Version(1, 56, 5),
                UpdateHostOs.MacOs,
                Architecture.Arm64,
                "osx-arm64");

            Assert.Equal(
                "https://github.com/lay295/TwitchDownloader/releases/download/1.56.5/TwitchDownloaderAvalonia-1.56.5-MacOSArm64.zip",
                url);
        }

        [Fact]
        public void ConstructsPackageNameLikeCli()
        {
            var name = UpdatePackageResolver.TryConstructPackageName(
                "TwitchDownloaderAvalonia-1.56.5-{0}.zip",
                UpdateHostOs.Linux,
                Architecture.X64,
                "linux-x64");

            Assert.Equal("TwitchDownloaderAvalonia-1.56.5-Linux-x64.zip", name);
        }
    }
}
