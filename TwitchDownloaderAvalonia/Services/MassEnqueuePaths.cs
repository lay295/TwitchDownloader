namespace TwitchDownloaderAvalonia.Services
{
    internal static class MassEnqueuePaths
    {
        public static string ChatRenderOutput(string chatFilename, string? videoFilename, string container)
        {
            var extension = container.Trim().TrimStart('.').ToLowerInvariant();
            var withoutGz = chatFilename.Replace(".gz", "");
            var output = Path.ChangeExtension(withoutGz, "." + extension);
            if (SamePath(output, chatFilename) || (videoFilename is not null && SamePath(output, videoFilename)))
                output = Path.ChangeExtension(withoutGz, " - CHAT." + extension);

            return output;
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.Ordinal);
        }
    }
}