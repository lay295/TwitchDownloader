namespace TwitchDownloaderCore.Models
{
    public readonly record struct Resolution(uint Width, uint Height)
    {
        public Resolution(uint height) : this(0, height) { }

        public bool HasWidth => Width > 0;

        public override string ToString() => $"{Width}x{Height}";

        public static implicit operator Resolution((uint width, uint height) tuple) => new(tuple.width, tuple.height);

        public static implicit operator Resolution(M3U8.Stream.ExtStreamInfo.StreamResolution sr) => new(sr.Width, sr.Height);

        public static implicit operator M3U8.Stream.ExtStreamInfo.StreamResolution(Resolution res) => new(res.Width, res.Height);
    }
}