namespace TwitchDownloaderCore.Models.Interfaces
{
    public interface IVideoQuality<out TItem>
    {
        public TItem Item { get; }

        public string Name { get; }

        public Resolution Resolution { get; }

        public decimal Framerate { get; }

        public bool IsSource { get; }

        public string Path { get; }

        public string ToString() => Name;
    }
}