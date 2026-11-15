namespace TwitchDownloaderCLI.Tests.CliTestHelpers
{
    /// <summary>
    /// Tests that touch process-wide state (Console.Out/In, the current directory, environment variables such as PATH/TMPDIR)
    /// must be in this collection. It disables parallelization so no other test observes or clobbers that state mid-test.
    /// </summary>
    [CollectionDefinition(NAME, DisableParallelization = true)]
    public sealed class ProcessStateCollection
    {
        public const string NAME = "CLI process-wide state";
    }

    /// <summary>
    /// Redirects <see cref="Console.Out"/> (and optionally <see cref="Console.In"/>) for the lifetime of a test and restores the originals on dispose.
    /// Output always uses <c>\n</c> line endings so assertions are identical on Windows, Linux and macOS.
    /// </summary>
    public sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _originalOut = Console.Out;
        private readonly TextReader _originalIn = Console.In;
        private readonly StringWriter _out = new() { NewLine = "\n" };

        /// <param name="input">Text that <see cref="Console.ReadLine"/> will return line by line, or null to leave stdin untouched.</param>
        public ConsoleCapture(string? input = null)
        {
            Console.SetOut(_out);
            if (input is not null)
            {
                Console.SetIn(new StringReader(input));
            }
        }

        public string Output => _out.ToString();

        public void Dispose()
        {
            Console.SetOut(_originalOut);
            Console.SetIn(_originalIn);
        }
    }

    /// <summary>Sets environment variables for the lifetime of a test and restores their previous values on dispose.</summary>
    public sealed class EnvironmentVariableOverride : IDisposable
    {
        private readonly Dictionary<string, string?> _originals = new();

        public EnvironmentVariableOverride(string value, params string[] names)
        {
            foreach (var name in names)
            {
                _originals[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originals)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    /// <summary>Changes <see cref="Environment.CurrentDirectory"/> for the lifetime of a test and restores it on dispose.</summary>
    /// <remarks>Dispose this before the directory itself is deleted: Windows cannot delete the current directory.</remarks>
    public sealed class WorkingDirectoryOverride : IDisposable
    {
        private readonly string _original = Environment.CurrentDirectory;

        public WorkingDirectoryOverride(string directory) => Environment.CurrentDirectory = directory;

        public void Dispose() => Environment.CurrentDirectory = _original;
    }
}
