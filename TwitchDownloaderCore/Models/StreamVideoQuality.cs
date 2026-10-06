using TwitchDownloaderCore.Models.Interfaces;

namespace TwitchDownloaderCore.Models
{
    public sealed class StreamVideoQuality : IVideoQuality<StreamQuality>
    {
        public StreamQuality Item { get; }
        public string Name { get; }

        public Resolution Resolution => Item.Resolution;

        public decimal Framerate => Item.Framerate;

        public bool IsSource => Item.Video.Equals("chunked", StringComparison.OrdinalIgnoreCase);

        public string Path => Item.Path;

        public int BitRate => Item.Bandwidth;

        public VideoOrientation Orientation => VideoOrientation.Landscape;

        internal StreamVideoQuality(StreamQuality item, string name)
        {
            Item = item;
            Name = name;
        }
    }
}
