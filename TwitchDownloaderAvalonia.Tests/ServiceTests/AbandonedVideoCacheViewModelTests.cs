using Microsoft.Extensions.Logging.Abstractions;
using TwitchDownloaderAvalonia.Tests.Fakes;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class AbandonedVideoCacheViewModelTests
    {
        [Fact]
        public void AgeUsesCreationTimeUtcDays()
        {
            using var cache = TempCache.Empty(createdUtc: DateTime.UtcNow.AddDays(-10).AddHours(-6));
            cache.Info.Refresh();
            var expected = (DateTime.UtcNow - cache.Info.CreationTimeUtc).Days;
            using var vm = Create(cache.Info);

            Assert.Equal(expected, vm.Items[0].Age);
            Assert.Equal(cache.Info.Name, vm.Items[0].FolderName);
            Assert.True(expected >= 10);
            Assert.False(vm.Items[0].ShouldDelete);
        }

        [Fact]
        public void EmptyDirectoryDefaultsToDeleteAfterSizeCalc()
        {
            using var cache = TempCache.Empty();
            using var vm = Create(cache.Info);

            Assert.False(vm.Items[0].ShouldDelete);
            vm.CalculateSizes();

            Assert.True(vm.Items[0].ShouldDelete);
            Assert.Equal(0, vm.Items[0].SizeBytes);
            Assert.Equal("0B", vm.Items[0].SizeText);
        }

        [Fact]
        public void NonEmptyDirectoryStaysUncheckedUntilUserSelects()
        {
            using var cache = TempCache.WithFile(100);
            using var vm = Create(cache.Info);

            vm.CalculateSizes();

            Assert.False(vm.Items[0].ShouldDelete);
            Assert.Equal(100, vm.Items[0].SizeBytes);
            Assert.Equal(VideoSizeEstimator.StringifyByteCount(100), vm.Items[0].SizeText);
        }

        [Fact]
        public void AcceptReturnsOnlyCheckedDirectories()
        {
            using var empty = TempCache.Empty();
            using var nonempty = TempCache.WithFile(50);
            DirectoryInfo[]? result = null;
            using var vm = Create(empty.Info, nonempty.Info, close: dirs => result = dirs);

            vm.CalculateSizes();
            Assert.True(vm.Items[0].ShouldDelete);
            Assert.False(vm.Items[1].ShouldDelete);

            vm.Items[1].ShouldDelete = true;
            vm.AcceptCommand.Execute(null);

            Assert.NotNull(result);
            Assert.Equal(2, result!.Length);
            Assert.Contains(result, dir => dir.FullName == empty.Info.FullName);
            Assert.Contains(result, dir => dir.FullName == nonempty.Info.FullName);
        }

        [Fact]
        public void CancelAndCloseReturnEmpty()
        {
            using var cache = TempCache.Empty();
            using var vm = Create(cache.Info);

            vm.CalculateSizes();
            Assert.True(vm.Items[0].ShouldDelete);
            Assert.Empty(vm.Complete(accepted: false));
        }

        [Fact]
        public void TotalSizeAggregatesListedCaches()
        {
            using var first = TempCache.WithFile(100);
            using var second = TempCache.WithFile(250);
            using var empty = TempCache.Empty();
            using var vm = Create(first.Info, second.Info, empty.Info);

            Assert.Equal(0, vm.TotalSizeBytes);
            vm.CalculateSizes();

            Assert.Equal(350, vm.TotalSizeBytes);
            Assert.EndsWith(AbandonedVideoCacheItem.FormatSize(350), vm.TotalSizeText);
        }

        [Fact]
        public void SelectAllChecksEveryRow()
        {
            using var first = TempCache.WithFile(10);
            using var second = TempCache.WithFile(20);
            using var vm = Create(first.Info, second.Info);

            vm.CalculateSizes();
            vm.SelectAllCommand.Execute(null);

            Assert.All(vm.Items, item => Assert.True(item.ShouldDelete));
            Assert.Equal(2, vm.Complete(accepted: true).Length);
        }

        private static AbandonedVideoCacheViewModel Create(
            DirectoryInfo first,
            DirectoryInfo? second = null,
            DirectoryInfo? third = null,
            Action<DirectoryInfo[]>? close = null)
        {
            var dirs = new List<DirectoryInfo> { first };
            if (second is not null)
                dirs.Add(second);

            if (third is not null)
                dirs.Add(third);

            return new AbandonedVideoCacheViewModel(
                TestLocalization.Instance,
                dirs,
                close ?? (_ => { }),
                new FakeDialogService(),
                NullLogger.Instance);
        }

        private sealed class TempCache : IDisposable
        {
            public DirectoryInfo Info { get; }

            private TempCache(DirectoryInfo info) => Info = info;

            public static TempCache Empty(DateTime? createdUtc = null) => Create(withBytes: 0, createdUtc);

            public static TempCache WithFile(int bytes, DateTime? createdUtc = null) => Create(bytes, createdUtc);

            private static TempCache Create(int withBytes, DateTime? createdUtc)
            {
                var path = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", "vod-cache-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                if (withBytes > 0)
                    File.WriteAllBytes(Path.Combine(path, "part.ts"), new byte[withBytes]);

                var info = new DirectoryInfo(path);
                if (createdUtc is { } created)
                    info.CreationTimeUtc = created;

                return new TempCache(info);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Info.FullName))
                        Directory.Delete(Info.FullName, true);
                }
                catch
                {
                    // Best-effort cleanup for temp test dirs.
                }
            }
        }
    }
}
