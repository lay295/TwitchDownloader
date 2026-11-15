using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // Tests DisposableDictionary, which owns the chat renderer's caches of native SkiaSharp images; if it does not dispose
    // its values, every render leaks native memory.
    public class DisposableDictionaryTests
    {
        private sealed class Disposable : IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }

        /// <summary>Disposing the dictionary disposes every value exactly once, and tolerates null values.</summary>
        [Fact]
        public void Dispose_DisposesEveryValue()
        {
            var a = new Disposable();
            var b = new Disposable();
            var dictionary = new DisposableDictionary<string, Disposable> { ["a"] = a, ["b"] = b, ["null"] = null! };

            dictionary.Dispose();

            Assert.Equal(1, a.DisposeCount);
            Assert.Equal(1, b.DisposeCount);
        }
    }
}
