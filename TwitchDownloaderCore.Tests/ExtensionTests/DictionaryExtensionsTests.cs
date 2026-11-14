using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests DictionaryExtensions.AddRange, used by ChatRenderer to bulk-load its badge/emote/emoji/avatar caches.
    public class DictionaryExtensionsTests
    {
        /// <summary>The selector overload must key and value every item with the given selectors (e.g. emotes keyed by id).</summary>
        [Fact]
        public void AddRange_WithSelectors_AddsEveryItem()
        {
            var dictionary = new Dictionary<int, string>();

            dictionary.AddRange(["a", "bb", "ccc"], x => x.Length, x => x.ToUpperInvariant());

            Assert.Equal(new Dictionary<int, string> { [1] = "A", [2] = "BB", [3] = "CCC" }, dictionary);
        }

        /// <summary>The dictionary overload must copy every pair into the target without dropping existing entries.</summary>
        [Fact]
        public void AddRange_FromDictionary_CopiesEveryPair()
        {
            var dictionary = new Dictionary<string, int> { ["existing"] = 0 };

            dictionary.AddRange(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

            Assert.Equal(new Dictionary<string, int> { ["existing"] = 0, ["a"] = 1, ["b"] = 2 }, dictionary);
        }

        /// <summary>
        /// Both overloads use Add, not the indexer, so a duplicate key throws instead of silently replacing a cached emote.
        /// Guards against a refactor to the indexer hiding duplicate-name bugs.
        /// </summary>
        [Fact]
        public void AddRange_ThrowsOnDuplicateKey()
        {
            var dictionary = new Dictionary<string, int> { ["a"] = 0 };

            Assert.Throws<ArgumentException>(() => dictionary.AddRange(new Dictionary<string, int> { ["a"] = 1 }));
            Assert.Throws<ArgumentException>(() => dictionary.AddRange(["a"], x => x, _ => 1));
        }
    }
}
