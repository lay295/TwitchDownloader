namespace TwitchDownloaderCore.Models.Interfaces
{
    public interface IVideoQuality<out TItem>
    {
        TItem Item { get; }

        string Name { get; }

        Resolution Resolution { get; }

        decimal Framerate { get; }

        bool IsSource { get; }

        string Path { get; }

        string ToString() => Name;
    }
}