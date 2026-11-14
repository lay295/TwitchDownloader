using System.Text.Json;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.TwitchObjectTests
{
    // Tests ChatRootInfo/ChatRootVersion, the "FileInfo" header of chat JSON files. ChatJson compares these versions to pick
    // the right upgrade path (e.g. legacy badge format for <= 1.2.2), so the comparison operators and defaults must be exact.
    public class ChatRootInfoTests
    {
        /// <summary>
        /// Files written before FileInfo existed get the defaults: version 1.0.0 (so every legacy upgrade path applies) and
        /// DateTime.FromBinary(0) timestamps.
        /// </summary>
        [Fact]
        public void Defaults_DescribeAPreVersioningFile()
        {
            var info = new ChatRootInfo();

            Assert.Equal(new ChatRootVersion(1, 0, 0), info.Version);
            Assert.Equal(new ChatRootVersion(1, 0, 0), new ChatRootVersion());
            Assert.Equal(DateTime.FromBinary(0), info.CreatedAt);
            Assert.Equal(DateTime.FromBinary(0), info.UpdatedAt);
        }

        /// <summary>
        /// The version written into new files. Bumping it is a deliberate schema change, so this test makes that visible
        /// (and reminds to add a ChatJson upgrade test for the old version).
        /// </summary>
        [Fact]
        public void CurrentVersion_Is140()
        {
            Assert.Equal("1.4.0", ChatRootVersion.CurrentVersion.ToString());
        }

        /// <summary>
        /// Versions compare component by component (major, then minor, then patch), not lexically, so 1.10.0 is newer than 1.9.9.
        /// </summary>
        [Theory]
        [InlineData(1, 2, 3, 1, 2, 3, 0)]
        [InlineData(1, 2, 3, 1, 2, 2, 1)]
        [InlineData(1, 3, 0, 1, 2, 9, 1)]
        [InlineData(2, 0, 0, 1, 9, 9, 1)]
        [InlineData(1, 10, 0, 1, 9, 9, 1)]
        [InlineData(1, 2, 2, 1, 3, 0, -1)]
        [InlineData(0, 9, 9, 1, 0, 0, -1)]
        public void ComparisonOperators_CompareComponentWise(uint aMajor, uint aMinor, uint aPatch, uint bMajor, uint bMinor, uint bPatch, int expectedSign)
        {
            var a = new ChatRootVersion(aMajor, aMinor, aPatch);
            var b = new ChatRootVersion(bMajor, bMinor, bPatch);

            Assert.Equal(expectedSign > 0, a > b);
            Assert.Equal(expectedSign < 0, a < b);
            Assert.Equal(expectedSign >= 0, a >= b);
            Assert.Equal(expectedSign <= 0, a <= b);
            Assert.Equal(expectedSign == 0, a == b);
            Assert.Equal(expectedSign == 0, a.GetHashCode() == b.GetHashCode());
        }

        /// <summary>
        /// FileInfo uses init-only properties and must still round-trip through System.Text.Json with the PascalCase names
        /// found in every chat file ("Version": {"Major", "Minor", "Patch"}, "CreatedAt", "UpdatedAt").
        /// </summary>
        [Fact]
        public void Json_RoundTripsWithPascalCaseNames()
        {
            const string JSON = """{"Version":{"Major":1,"Minor":3,"Patch":1},"CreatedAt":"2024-06-24T18:56:06Z","UpdatedAt":"0001-01-01T00:00:00"}""";

            var info = JsonSerializer.Deserialize<ChatRootInfo>(JSON)!;

            Assert.Equal(new ChatRootVersion(1, 3, 1), info.Version);
            Assert.Equal(new DateTime(2024, 6, 24, 18, 56, 6, DateTimeKind.Utc), info.CreatedAt);
            Assert.Equal(JSON, JsonSerializer.Serialize(info));
        }
    }
}
