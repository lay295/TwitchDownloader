using System.Text.Json;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests JsonElementExtensions.DeserializeFirstAndLastFromList, which ChatJson uses to read only the first and last
    // comment of huge chat files (to get the chat's time range without deserializing every comment).
    public class JsonElementExtensionsTests
    {
        private sealed record Item(int id);

        private static List<Item> Deserialize(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.DeserializeFirstAndLastFromList<Item>();
        }

        /// <summary>For 3+ elements only the first and the last are returned, in that order; the middle ones are skipped.</summary>
        [Fact]
        public void ReturnsFirstAndLast_ForLongArray()
        {
            var result = Deserialize("""[{"id":1},{"id":2},{"id":3},{"id":4}]""");

            Assert.Equal([new Item(1), new Item(4)], result);
        }

        /// <summary>
        /// A single-element array must yield one item, not the same item twice; a chat with one comment
        /// would otherwise appear to have two.
        /// </summary>
        [Fact]
        public void ReturnsOneItem_ForSingleElementArray()
        {
            var result = Deserialize("""[{"id":7}]""");

            Assert.Equal([new Item(7)], result);
        }

        /// <summary>An empty comments array must give an empty list rather than throwing or yielding a default item.</summary>
        [Fact]
        public void ReturnsEmpty_ForEmptyArray()
        {
            Assert.Empty(Deserialize("[]"));
        }

        /// <summary>The serializer options must be honored, because ChatJson passes custom options (e.g. case-insensitivity).</summary>
        [Fact]
        public void UsesSuppliedSerializerOptions()
        {
            using var document = JsonDocument.Parse("""[{"ID":1},{"ID":2}]""");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var result = document.RootElement.DeserializeFirstAndLastFromList<Item>(options);

            Assert.Equal([new Item(1), new Item(2)], result);
        }
    }
}
