using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal sealed class RollingFileLoggerProvider : ILoggerProvider
    {
        private const long MAX_FILE_BYTES = 2 * 1024 * 1024;

        private readonly string _archivePath;
        private readonly Lock _gate = new();
        private StreamWriter? _writer;
        private bool _disposed;

        public RollingFileLoggerProvider(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            FilePath = filePath;
            var directory = Path.GetDirectoryName(filePath);
            _archivePath = Path.Combine(directory ?? ".", "avalonia.1.log");
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        public string FilePath { get; }

        public ILogger CreateLogger(string categoryName) => new RollingFileLogger(this, categoryName);

        internal void Write(string category, MsLogLevel level, string message, Exception? exception)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                EnsureWriter();
                _writer!.WriteLine($"{DateTimeOffset.Now:O} {level} {category} {message}");
                if (exception is not null)
                    _writer.WriteLine(exception);
                _writer.Flush();
                MaybeRoll();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _writer?.Dispose();
                _writer = null;
            }
        }

        private void EnsureWriter()
        {
            _writer ??= new StreamWriter(new FileStream(
                FilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read))
            {
                AutoFlush = true,
            };
        }

        private void MaybeRoll()
        {
            if (_writer?.BaseStream.Length <= MAX_FILE_BYTES)
                return;

            _writer?.Dispose();
            _writer = null;
            if (File.Exists(_archivePath))
                File.Delete(_archivePath);

            if (File.Exists(FilePath))
                File.Move(FilePath, _archivePath);
        }
    }

    file sealed class RollingFileLogger(RollingFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(MsLogLevel logLevel) => logLevel is not MsLogLevel.None;

        public void Log<TState>(
            MsLogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
