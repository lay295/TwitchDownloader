using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.ModelTests
{
    // Tests CollisionBehavior, whose numeric values are persisted as an int in user.config (Settings.FileCollisionBehavior) and cast back
    // by FileCollisionService. Renumbering it would silently change every existing user's choice, e.g. turning "Rename" into "Cancel".
    public class CollisionBehaviorTests
    {
        // Verifies the persisted numeric value of each member.
        // Why: guards against reordering or inserting members, which compiles fine but reinterprets saved settings.
        [Theory]
        [InlineData(nameof(CollisionBehavior.Prompt), 0)]
        [InlineData(nameof(CollisionBehavior.Overwrite), 1)]
        [InlineData(nameof(CollisionBehavior.Rename), 2)]
        [InlineData(nameof(CollisionBehavior.Cancel), 3)]
        public void Member_HasStablePersistedValue(string member, int persisted)
        {
            Assert.Equal(persisted, (int)Enum.Parse<CollisionBehavior>(member));
        }

        // Verifies the FileCollisionBehavior setting defaults to Prompt.
        // Why: a fresh install must ask before touching an existing file; a default of Overwrite would destroy data without warning.
        [Fact]
        public void DefaultSetting_IsPrompt()
        {
            var defaultValue = int.Parse(SettingDefaults.Of(nameof(Settings.FileCollisionBehavior)));

            Assert.Equal(CollisionBehavior.Prompt, (CollisionBehavior)defaultValue);
        }
    }
}
