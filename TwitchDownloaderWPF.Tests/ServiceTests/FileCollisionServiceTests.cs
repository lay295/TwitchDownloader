using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.ServiceTests
{
    // Tests FileCollisionService, the callback every GUI download/render passes to Core to decide what happens when the output file
    // already exists. Getting it wrong either silently destroys an existing recording (overwrite) or silently drops a queued task (cancel).
    // Only the non-interactive paths are tested: the Prompt path shows an Ookii TaskDialog and needs a real window.
    [Collection(ProcessStateCollection.NAME)]
    public sealed class FileCollisionServiceTests : IDisposable
    {
        private readonly int _originalSetting = Settings.Default.FileCollisionBehavior;
        private readonly TempDirectory _tempDir = new();
        private readonly FileInfo _existingFile;

        public FileCollisionServiceTests()
        {
            FileCollisionService._sessionCollisionBehavior = null;
            _existingFile = new FileInfo(_tempDir.Combine("video.mp4"));
            File.WriteAllText(_existingFile.FullName, "existing recording");
        }

        public void Dispose()
        {
            Settings.Default.FileCollisionBehavior = _originalSetting;
            FileCollisionService._sessionCollisionBehavior = null;
            _tempDir.Dispose();
        }

        // Verifies the Overwrite setting hands Core the same path without prompting.
        // Why: users who chose "always overwrite" must not get a dialog per file during a mass download.
        [Fact]
        public void OverwriteSetting_ReturnsSameFile()
        {
            Settings.Default.FileCollisionBehavior = (int)CollisionBehavior.Overwrite;

            var result = FileCollisionService.HandleCollisionCallback(_existingFile, null!);

            Assert.Equal(_existingFile.FullName, result!.FullName);
        }

        // Verifies the Rename setting returns "name (1).ext" next to the original, which does not exist yet.
        // Why: Rename is the safe choice for keeping old recordings; returning the original path (or one that also exists) would overwrite them.
        [Fact]
        public void RenameSetting_ReturnsNonCollidingSibling()
        {
            Settings.Default.FileCollisionBehavior = (int)CollisionBehavior.Rename;

            var result = FileCollisionService.HandleCollisionCallback(_existingFile, null!);

            Assert.Equal(_tempDir.Combine("video (1).mp4"), result!.FullName);
            Assert.False(result.Exists);
            Assert.Equal("existing recording", File.ReadAllText(_existingFile.FullName));
        }

        // Verifies the Cancel setting returns null, which Core turns into "abort this task".
        // Why: returning anything else would make Core write over the existing file despite the user's choice.
        [Fact]
        public void CancelSetting_ReturnsNull()
        {
            Settings.Default.FileCollisionBehavior = (int)CollisionBehavior.Cancel;

            Assert.Null(FileCollisionService.HandleCollisionCallback(_existingFile, null!));
        }

        // Verifies a choice remembered via "Remember my choice" is used instead of prompting, even though the setting is still Prompt.
        // Why: the remembered choice is what stops a 50-video mass download from showing 50 dialogs; if the setting won, the dialog
        // (which needs a real owner window, null here) would be shown again.
        [Fact]
        public void RememberedSessionChoice_OverridesPromptSetting()
        {
            Settings.Default.FileCollisionBehavior = (int)CollisionBehavior.Prompt;
            FileCollisionService._sessionCollisionBehavior = CollisionBehavior.Cancel;

            Assert.Null(FileCollisionService.HandleCollisionCallback(_existingFile, null!));
        }
    }
}
