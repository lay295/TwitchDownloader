namespace TwitchDownloaderWPF.Tests.TestHelpers
{
    /// <summary>
    /// Tests that change process-wide state (the current directory, <c>Settings.Default</c> values, private static fields) must be in
    /// this collection. It disables parallelization so no other test observes the temporary change.
    /// </summary>
    [CollectionDefinition(NAME, DisableParallelization = true)]
    public sealed class ProcessStateCollection
    {
        public const string NAME = "WPF process-wide state";
    }
}
