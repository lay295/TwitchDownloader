using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.Tests.TestHelpers;
using TwitchDownloaderWPF.TwitchTasks;

namespace TwitchDownloaderWPF.Tests.TaskTests
{
    // Tests ChatDownloadTask's handling of the "delay until the stream ends" option (the shared lifecycle is covered by TwitchTaskTests).
    // Chat downloads take either a numeric VOD ID or a clip slug; only VODs can be live, so the delay must only apply to numeric IDs.
    [Collection(StaticHttpClientCollection.NAME)]
    public sealed class ChatDownloadTaskTests : IDisposable
    {
        private readonly TempDirectory _tempDir = new();

        public void Dispose() => _tempDir.Dispose();

        // Verifies a clip chat download with DelayDownload set goes straight to downloading: it never enters Waiting and never polls GQL.
        // (The download itself is stopped offline by a cancelling file-collision callback, which is why it ends Failed.)
        // Why: the queue's delay checkbox applies to every item of a mass download, clips included; a clip slug cannot be parsed as a video ID,
        // so trying to monitor it would fail the task or poll Twitch with a bogus ID.
        [Fact]
        public async Task DelayDownload_IsIgnoredForClipSlugs()
        {
            var handler = FakeGql.VideoStatus("RECORDING");
            using var _ = FakeGql.Use(handler);
            var output = _tempDir.Combine("chat.json");
            File.WriteAllText(output, "existing");
            var task = new ChatDownloadTask
            {
                DownloadOptions = new ChatDownloadOptions { Id = "SomeClipSlug", DelayDownload = true, Filename = output, TempFolder = _tempDir.Path, FileCollisionCallback = _ => null }
            };
            var statuses = new List<TwitchTaskStatus>();
            task.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TwitchTask.Status)) statuses.Add(task.Status);
            };

            await task.RunIgnoringGcCollectBugAsync();

            Assert.DoesNotContain(TwitchTaskStatus.Waiting, statuses);
            Assert.Empty(handler.Requests);
            Assert.Equal([TwitchTaskStatus.Running, TwitchTaskStatus.Failed], statuses);
        }
    }
}
