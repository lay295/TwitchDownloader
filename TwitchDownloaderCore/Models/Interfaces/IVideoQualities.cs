using System.Diagnostics.CodeAnalysis;

namespace TwitchDownloaderCore.Models.Interfaces
{
    public interface IVideoQualities<out T> : IEnumerable<IVideoQuality<T>>
    {
        IReadOnlyList<IVideoQuality<T>> Qualities { get; }

        [return: MaybeNull]
        IVideoQuality<T> GetQuality([AllowNull] string qualityString);

        [return: MaybeNull]
        IVideoQuality<T> BestQuality();

        [return: MaybeNull]
        IVideoQuality<T> WorstQuality();
    }
}