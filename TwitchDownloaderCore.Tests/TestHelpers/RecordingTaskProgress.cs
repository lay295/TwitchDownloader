using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tests.TestHelpers
{
    /// <summary>
    /// An <see cref="ITaskProgress"/> that records every log line, status and progress report,
    /// so tests can assert that production code warned/errored/reported progress as expected.
    /// </summary>
    public sealed class RecordingTaskProgress : ITaskProgress
    {
        public ConcurrentQueue<string> Verbose { get; } = new();
        public ConcurrentQueue<string> Info { get; } = new();
        public ConcurrentQueue<string> Warnings { get; } = new();
        public ConcurrentQueue<string> Errors { get; } = new();
        public ConcurrentQueue<string> Ffmpeg { get; } = new();
        public ConcurrentQueue<string> Statuses { get; } = new();
        public ConcurrentQueue<int> Percentages { get; } = new();

        public void LogVerbose(string logMessage) => Verbose.Enqueue(logMessage);
        public void LogVerbose(DefaultInterpolatedStringHandler logMessage) => Verbose.Enqueue(logMessage.ToStringAndClear());
        public void LogInfo(string logMessage) => Info.Enqueue(logMessage);
        public void LogInfo(DefaultInterpolatedStringHandler logMessage) => Info.Enqueue(logMessage.ToStringAndClear());
        public void LogWarning(string logMessage) => Warnings.Enqueue(logMessage);
        public void LogWarning(DefaultInterpolatedStringHandler logMessage) => Warnings.Enqueue(logMessage.ToStringAndClear());
        public void LogError(string logMessage) => Errors.Enqueue(logMessage);
        public void LogError(DefaultInterpolatedStringHandler logMessage) => Errors.Enqueue(logMessage.ToStringAndClear());
        public void LogFfmpeg(string logMessage) => Ffmpeg.Enqueue(logMessage);

        public void SetStatus(string status) => Statuses.Enqueue(status);
        public void SetTemplateStatus(string statusTemplate, int initialPercent) => Statuses.Enqueue(statusTemplate);
        public void SetTemplateStatus(string statusTemplate, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2) => Statuses.Enqueue(statusTemplate);
        public void ReportProgress(int percent) => Percentages.Enqueue(percent);
        public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2) => Percentages.Enqueue(percent);
    }
}
