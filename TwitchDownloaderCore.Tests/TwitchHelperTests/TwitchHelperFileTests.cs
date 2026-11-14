using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper's file-system helpers: ClaimFile / CleanUpClaimedFile (output-file collision handling used by every
    // download/render task), CreateDirectory / Set777UnixFilePermissions (shared cache folders) and CleanupAbandonedVideoCaches.
    // Mistakes here overwrite user files, leave empty junk outputs behind, or delete the wrong cache folders.
    public class TwitchHelperFileTests
    {
        private static Func<FileInfo, FileInfo> MustNotBeCalled => _ => throw new InvalidOperationException("Callback must not be called for a new file");

        // A new output path is returned as an absolute path and its missing parent directories are created, without asking about collisions.
        [Fact]
        public void ClaimFile_NewFile_ReturnsFullPathAndCreatesParentDirectory()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("sub", "dir", "out.mp4");

            var file = TwitchHelper.ClaimFile(path, MustNotBeCalled, new RecordingTaskProgress());

            Assert.Equal(Path.GetFullPath(path), file.FullName);
            Assert.True(Directory.Exists(temp.Combine("sub", "dir")));
            Assert.False(file.Exists);
        }

        // When the file exists the callback decides the destination (e.g. "Rename"); a different destination is logged and its folder created.
        [Fact]
        public void ClaimFile_Existing_CallbackRename_IsLoggedAndUsed()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("out.mp4");
            File.WriteAllText(path, "existing");
            var renamed = new FileInfo(temp.Combine("renamed", "out (1).mp4"));
            var progress = new RecordingTaskProgress();

            var file = TwitchHelper.ClaimFile(path, existing => { Assert.Equal(path, existing.FullName); return renamed; }, progress);

            Assert.Equal(renamed.FullName, file.FullName);
            Assert.Contains($"'{path}' will be renamed to '{renamed.FullName}'", progress.Info);
            Assert.True(Directory.Exists(temp.Combine("renamed")));
        }

        // Choosing "Overwrite" (callback returns the same file) must not log a rename.
        [Fact]
        public void ClaimFile_Existing_CallbackOverwrite_DoesNotLogRename()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("out.mp4");
            File.WriteAllText(path, "existing");
            var progress = new RecordingTaskProgress();

            var file = TwitchHelper.ClaimFile(path, existing => existing, progress);

            Assert.Equal(path, file.FullName);
            Assert.Empty(progress.Info);
        }

        // Choosing "Cancel" (callback returns null) must abort with FileNotFoundException rather than overwrite the user's file.
        [Fact]
        public void ClaimFile_Existing_CallbackCancel_Throws()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("out.mp4");
            File.WriteAllText(path, "existing");

            Assert.Throws<FileNotFoundException>(() => TwitchHelper.ClaimFile(path, _ => null!, new RecordingTaskProgress()));
            Assert.Equal("existing", File.ReadAllText(path));
        }

        // A missing callback is a programming error that must be visible (warning) but falls back to the original path.
        [Fact]
        public void ClaimFile_Existing_NullCallback_WarnsAndKeepsPath()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("out.mp4");
            File.WriteAllText(path, "existing");
            var progress = new RecordingTaskProgress();

            var file = TwitchHelper.ClaimFile(path, null!, progress);

            Assert.Equal(path, file.FullName);
            Assert.Contains("fileAlreadyExistsCallback was null.", progress.Warnings);
        }

        // A claimed output that stayed empty (task failed/cancelled before writing) must be closed and deleted so no 0-byte junk is left.
        [Fact]
        public void CleanUpClaimedFile_EmptyFile_ClosesStreamAndDeletes()
        {
            using var temp = new TempDirectory();
            var file = new FileInfo(temp.Combine("out.mp4"));
            var stream = file.Create();

            TwitchHelper.CleanUpClaimedFile(file, stream, new RecordingTaskProgress());

            Assert.False(File.Exists(file.FullName));
            Assert.False(stream.CanWrite); // disposed
        }

        // Outputs with content must be kept untouched (and their stream left open for the caller).
        [Fact]
        public void CleanUpClaimedFile_NonEmptyFile_IsKept()
        {
            using var temp = new TempDirectory();
            var file = new FileInfo(temp.Combine("out.mp4"));
            using var stream = file.Create();
            stream.WriteByte(42);
            stream.Flush();

            TwitchHelper.CleanUpClaimedFile(file, stream, new RecordingTaskProgress());

            Assert.True(File.Exists(file.FullName));
            Assert.True(stream.CanWrite);
        }

        // Null or already-missing files are no-ops (cleanup runs in finally blocks after any kind of failure).
        [Fact]
        public void CleanUpClaimedFile_NullOrMissing_DoesNothing()
        {
            using var temp = new TempDirectory();
            var progress = new RecordingTaskProgress();

            TwitchHelper.CleanUpClaimedFile(null, null, progress);
            TwitchHelper.CleanUpClaimedFile(new FileInfo(temp.Combine("missing.mp4")), null, progress);

            Assert.Empty(progress.Warnings);
        }

        // CreateDirectory must create nested folders, be idempotent, and on Linux/macOS make the folder world-writable (777) so caches
        // can be shared between users/processes (e.g. the CLI under different accounts).
        [Fact]
        public void CreateDirectory_CreatesNestedAndSets777OnUnix()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("a", "b");

            var dir = TwitchHelper.CreateDirectory(path);
            var again = TwitchHelper.CreateDirectory(path, new RecordingTaskProgress());

            Assert.True(dir.Exists);
            Assert.Equal(dir.FullName, again.FullName);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(AllPermissions, File.GetUnixFileMode(path));
            }
        }

        private const UnixFileMode AllPermissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                                    | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                                                    | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        // Set777UnixFilePermissions must grant rwx to everyone and return the refreshed info. Unix only (the API throws on Windows).
        [Fact]
        public void Set777UnixFilePermissions_GrantsAllPermissions()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are not supported on Windows");
#pragma warning disable CA1416 // Guarded by the SkipWhen above
            using var temp = new TempDirectory();
            var path = temp.Combine("file.txt");
            File.WriteAllText(path, "x");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var info = new FileInfo(path);
            var result = TwitchHelper.Set777UnixFilePermissions(info);

            Assert.Same(info, result);
            Assert.Equal(AllPermissions, result.UnixFileMode);
#pragma warning restore CA1416
        }

        // ---------------- CleanupAbandonedVideoCaches ----------------

        // Creates a cache sub-directory (with a file inside) whose last write time is `age` ago.
        private static string MakeCacheDir(TempDirectory root, string name, TimeSpan age)
        {
            var path = root.Combine(name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "part.ts"), "x");
            Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
            return path;
        }

        // Only "{videoId}_{ticks}" folders older than 7 days may be offered for deletion; recent caches (possibly in use by a running download)
        // and unrelated folders must never be offered. Folders the callback approves are deleted recursively and the count logged.
        [Fact]
        public async Task CleanupAbandonedVideoCaches_DeletesOnlyApprovedOldVideoCaches()
        {
            using var root = new TempDirectory();
            var old = MakeCacheDir(root, "2123456789_638400000000000000", TimeSpan.FromDays(8));
            var recent = MakeCacheDir(root, "987654321_638500000000000000", TimeSpan.FromDays(1));
            var unrelatedOld = MakeCacheDir(root, "emotes", TimeSpan.FromDays(30));
            var notNumericOld = MakeCacheDir(root, "123_abc", TimeSpan.FromDays(30));
            DirectoryInfo[]? offered = null;
            var progress = new RecordingTaskProgress();

            await TwitchHelper.CleanupAbandonedVideoCaches(root.Path, dirs => offered = dirs, progress);

            Assert.Equal(old, Assert.Single(offered!).FullName);
            Assert.False(Directory.Exists(old));
            Assert.True(Directory.Exists(recent));
            Assert.True(Directory.Exists(unrelatedOld));
            Assert.True(Directory.Exists(notNumericOld));
            Assert.Contains("1 old video caches were deleted.", progress.Info);
        }

        // Declining (null or empty selection) must delete nothing and log nothing.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CleanupAbandonedVideoCaches_Declined_DeletesNothing(bool returnNull)
        {
            using var root = new TempDirectory();
            var old = MakeCacheDir(root, "1_2", TimeSpan.FromDays(8));
            var progress = new RecordingTaskProgress();

            await TwitchHelper.CleanupAbandonedVideoCaches(root.Path, _ => returnNull ? null! : [], progress);

            Assert.True(Directory.Exists(old));
            Assert.Empty(progress.Info);
        }

        // Folders that can't be deleted are reported (verbose per folder + summary count) without stopping the rest.
        [Fact]
        public async Task CleanupAbandonedVideoCaches_PartialFailure_IsReported()
        {
            using var root = new TempDirectory();
            var old = MakeCacheDir(root, "1_2", TimeSpan.FromDays(8));
            var vanished = new DirectoryInfo(root.Combine("3_4")); // never created -> Directory.Delete throws
            var progress = new RecordingTaskProgress();

            await TwitchHelper.CleanupAbandonedVideoCaches(root.Path, dirs => [vanished, .. dirs], progress);

            Assert.False(Directory.Exists(old));
            Assert.Contains(progress.Verbose, v => v.StartsWith($"Could not delete '{vanished.FullName}'"));
            Assert.Contains("1 old video caches were deleted, 1 could not be deleted.", progress.Info);
        }

        // A missing cache folder, a cache with nothing old, or a null callback must never invoke the callback (it may show a UI prompt).
        [Fact]
        public async Task CleanupAbandonedVideoCaches_NothingToDo_DoesNotPrompt()
        {
            using var root = new TempDirectory();
            MakeCacheDir(root, "1_2", TimeSpan.FromHours(1));
            var prompted = false;
            var progress = new RecordingTaskProgress();

            await TwitchHelper.CleanupAbandonedVideoCaches(root.Combine("missing"), _ => { prompted = true; return []; }, progress);
            await TwitchHelper.CleanupAbandonedVideoCaches(root.Path, _ => { prompted = true; return []; }, progress);
            await TwitchHelper.CleanupAbandonedVideoCaches(root.Path, null!, progress);

            Assert.False(prompted);
            Assert.Contains("itemsToDeleteCallback was null.", progress.Warnings);
        }
    }
}
