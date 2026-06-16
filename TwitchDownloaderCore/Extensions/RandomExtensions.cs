namespace TwitchDownloaderCore.Extensions
{
    public static class RandomExtensions
    {
        extension(Random random)
        {
            public double NextDouble(double min, double max)
            {
                return random.NextDouble() * (max - min) + min;
            }
        }
    }
}