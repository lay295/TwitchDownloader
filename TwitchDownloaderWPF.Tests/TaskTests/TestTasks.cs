using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF.Tests.TaskTests
{
    /// <summary>
    /// Builds each concrete <see cref="TwitchTask"/> configured so that, if its Core worker is started, it fails within milliseconds without
    /// touching the network: chat downloads get an empty ID, chat update/render get a missing input file, and clip/VOD downloads target an
    /// existing file whose collision callback cancels. Every task's output path is <c>&lt;dir&gt;/&lt;TaskType&gt;.out</c>.
    /// </summary>
    internal static class TestTasks
    {
        public static TheoryData<string> AllTypes => new(
            nameof(ChatDownloadTask), nameof(ChatRenderTask), nameof(ChatUpdateTask), nameof(ClipDownloadTask), nameof(VodDownloadTask));

        public static string OutputPath(TempDirectory dir, string type) => dir.Combine($"{type}.out");

        /// <summary>
        /// Awaits <see cref="TwitchTask.RunAsync"/>, ignoring only the ArgumentOutOfRangeException that every run reaching its end currently
        /// throws from its final <c>GC.Collect(-1, ...)</c> (see TwitchTaskTests.RunAsync_CompletesWithoutThrowing). The task's final status is
        /// set before that call, so state assertions after this remain meaningful, and this keeps working once the bug is fixed.
        /// </summary>
        public static async Task RunIgnoringGcCollectBugAsync(this TwitchTask task)
        {
            try
            {
                await task.RunAsync();
            }
            catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "generation") { }
        }

        public static TwitchTask CreateOfflineFailing(string type, TempDirectory dir)
        {
            var output = OutputPath(dir, type);
            var missingInput = dir.Combine("missing.json");

            switch (type)
            {
                case nameof(ChatDownloadTask):
                    return new ChatDownloadTask { DownloadOptions = new ChatDownloadOptions { Id = "", Filename = output, TempFolder = dir.Path } };
                case nameof(ChatRenderTask):
                    return new ChatRenderTask { DownloadOptions = new ChatRenderOptions { InputFile = missingInput, OutputFile = output, TempFolder = dir.Path } };
                case nameof(ChatUpdateTask):
                    return new ChatUpdateTask { UpdateOptions = new ChatUpdateOptions { InputFile = missingInput, OutputFile = output, TempFolder = dir.Path } };
                case nameof(ClipDownloadTask):
                    File.WriteAllText(output, "existing");
                    return new ClipDownloadTask { DownloadOptions = new ClipDownloadOptions { Id = "SomeClipSlug", Filename = output, TempFolder = dir.Path, FileCollisionCallback = _ => null } };
                case nameof(VodDownloadTask):
                    File.WriteAllText(output, "existing");
                    return new VodDownloadTask { DownloadOptions = new VideoDownloadOptions { Id = 1, Filename = output, TempFolder = dir.Path, FileCollisionCallback = _ => null } };
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
