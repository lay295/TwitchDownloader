using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TwitchDownloaderCore.Models.Interfaces
{
    public interface IVideoQualities<out T> : IEnumerable<IVideoQuality<T>>
    {
        public IReadOnlyList<IVideoQuality<T>> Qualities { get; }

        [return: MaybeNull]
        public IVideoQuality<T> GetQuality(string qualityString);

        [return: MaybeNull]
        public IVideoQuality<T> BestQuality();

        [return: MaybeNull]
        public IVideoQuality<T> WorstQuality();
    }
}