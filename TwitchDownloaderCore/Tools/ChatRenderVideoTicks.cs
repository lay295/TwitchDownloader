namespace TwitchDownloaderCore.Tools
{
    public static class ChatRenderVideoTicks
    {
        public static (int startTick, int totalTicks) Calculate(
            int startOverride,
            int endOverride,
            double videoStart,
            double videoEnd,
            int framerate)
        {
            var startSeconds = startOverride == -1
                ? (int)Math.Floor(videoStart)
                : startOverride;

            var startTick = startSeconds * framerate;
            var endTick = endOverride == -1
                ? (int)Math.Ceiling(videoEnd * framerate)
                : endOverride * framerate;

            return (startTick, endTick - startTick);
        }
    }
}
