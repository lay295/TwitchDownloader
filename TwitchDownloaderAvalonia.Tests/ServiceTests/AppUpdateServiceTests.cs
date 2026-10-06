using System.IO.Compression;
using System.Net;
using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class AppUpdateServiceTests
    {
        [Fact]
        public async Task ProbeReturnsTrueOnSuccess()
        {
            var handler = new StatusHandler(HttpStatusCode.OK);
            var service = new AppUpdateService(new HttpClient(handler, disposeHandler: false));

            Assert.True(await service.ProbePackageAsync("https://example.com/app.zip", TestContext.Current.CancellationToken));
            Assert.True(await service.ProbePackageAsync("https://example.com/app.zip", TestContext.Current.CancellationToken));
            Assert.Equal(1, handler.Calls);
        }

        [Fact]
        public async Task ProbeReturnsFalseOnNotFound()
        {
            var handler = new StatusHandler(HttpStatusCode.NotFound);
            var service = new AppUpdateService(new HttpClient(handler, disposeHandler: false));

            Assert.False(await service.ProbePackageAsync("https://example.com/missing.zip", TestContext.Current.CancellationToken));
        }

        [Fact]
        public void BackupExtractAndRestoreRoundTrip()
        {
            var root = Path.Combine(Path.GetTempPath(), "td-update-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var exePath = Path.Combine(root, "TwitchDownloaderAvalonia.exe");
                File.WriteAllText(exePath, "old");
                var backupPath = exePath + ".bak";

                AppUpdateService.BackupCurrentExecutable(exePath, backupPath);
                Assert.False(File.Exists(exePath));
                Assert.Equal("old", File.ReadAllText(backupPath));

                var zipPath = Path.Combine(root, "update.zip");
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("TwitchDownloaderAvalonia.exe");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("new");
                }

                AppUpdateService.ExtractZipFiles(zipPath, root, progress: null);
                Assert.Equal("new", File.ReadAllText(exePath));

                AppUpdateService.TryRestoreBackup(backupPath, exePath);
                Assert.Equal("old", File.ReadAllText(exePath));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public async Task SimulateInstallReportsPhasesWithoutTouchingFiles()
        {
            var root = Path.Combine(Path.GetTempPath(), "td-update-sim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var marker = Path.Combine(root, "keep.txt");
            File.WriteAllText(marker, "keep");
            try
            {
                var phases = new List<UpdateInstallPhase>();
                var progress = new Progress<UpdateInstallProgress>(p => phases.Add(p.Phase));
                await AppUpdateService.SimulateInstallAsync(progress, TestContext.Current.CancellationToken);

                Assert.Contains(UpdateInstallPhase.Downloading, phases);
                Assert.Contains(UpdateInstallPhase.Extracting, phases);
                Assert.Contains(UpdateInstallPhase.Finishing, phases);
                Assert.Equal("keep", File.ReadAllText(marker));
                Assert.Single(Directory.GetFiles(root));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new ByteArrayContent([]),
                });
            }
        }
    }
}
