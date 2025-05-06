namespace TwitchDownloaderCore.Models.Interfaces
{
    public interface IVideoQuality<out T>
    {
        public T Item { get; }

        public string Name { get; }

        public bool IsSource { get; }

        public string ToString() => Name;
    }
}