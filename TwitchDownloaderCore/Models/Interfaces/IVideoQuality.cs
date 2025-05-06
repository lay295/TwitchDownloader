namespace TwitchDownloaderCore.Models.Interfaces
{
    public interface IVideoQuality<out T>
    {
        public T Item { get; }

        public string Name { get; }

        public Resolution Resolution { get; }

        public bool IsSource { get; }

        public string ToString() => Name;
    }
}