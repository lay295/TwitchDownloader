using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    public sealed record CliResult(int ExitCode, string StdOut, string StdErr)
    {
        /// <summary>Everything the process printed, for assertion failure messages.</summary>
        public override string ToString() => $"exit code {ExitCode}\n--- stdout ---\n{StdOut}\n--- stderr ---\n{StdErr}";
    }

    /// <summary>
    /// Runs the real, built TwitchDownloaderCLI as a child process, the same way a user or script would.
    /// </summary>
    /// <remarks>
    /// The CLI project is referenced by the test project, so its apphost, dll, deps.json and runtimeconfig.json are copied next
    /// to the test binary. The apphost is used (rather than <c>dotnet TwitchDownloaderCLI.dll</c>) because the CLI writes its
    /// license files next to <see cref="Environment.ProcessPath"/>, which would otherwise be the shared dotnet install folder.
    /// </remarks>
    public static class Cli
    {
        /// <summary>A proxy nobody listens on: any HTTP request from an "offline" run fails immediately instead of reaching the internet.</summary>
        private const string DEAD_PROXY = "http://127.0.0.1:9";

        private static readonly string AppHost = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "TwitchDownloaderCLI.exe" : "TwitchDownloaderCLI");

        // <dotnet root>/shared/Microsoft.NETCore.App/<version>/ -> <dotnet root>, i.e. the runtime that is running the tests right now.
        private static readonly string DotnetRoot = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));

        /// <param name="workDir">Working directory of the CLI. Its "systemp" sub folder becomes the CLI's system temp folder, so the default cache never touches the real one.</param>
        /// <param name="args">The exact argument array, as a shell would pass it.</param>
        /// <param name="stdin">Text typed at prompts. Stdin is always closed afterwards, so an unexpected prompt can never hang the test.</param>
        /// <param name="offline">Route all HTTP through <see cref="DEAD_PROXY"/> so the test cannot depend on (or be slowed down by) the network.</param>
        /// <param name="env">Extra environment changes (null value removes the variable).</param>
        public static CliResult Run(string workDir, IEnumerable<string> args, string? stdin = null, bool offline = true, IReadOnlyDictionary<string, string?>? env = null, TimeSpan? timeout = null)
        {
            var psi = File.Exists(AppHost)
                ? new ProcessStartInfo(AppHost)
                : new ProcessStartInfo(Path.Combine(DotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")) { ArgumentList = { "exec", Path.Combine(AppContext.BaseDirectory, "TwitchDownloaderCLI.dll") } };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            psi.WorkingDirectory = workDir;
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            psi.Environment["DOTNET_ROOT"] = DotnetRoot;

            // Path.GetTempPath() reads TMPDIR on Unix and TMP/TEMP on Windows
            var sysTemp = Directory.CreateDirectory(Path.Combine(workDir, "systemp")).FullName;
            psi.Environment["TMPDIR"] = sysTemp;
            psi.Environment["TMP"] = sysTemp;
            psi.Environment["TEMP"] = sysTemp;

            if (offline)
            {
                foreach (var name in new[] { "http_proxy", "https_proxy", "all_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" })
                    psi.Environment[name] = DEAD_PROXY;
                psi.Environment.Remove("no_proxy");
                psi.Environment.Remove("NO_PROXY");
            }

            foreach (var (key, value) in env ?? new Dictionary<string, string?>())
            {
                if (value is null) psi.Environment.Remove(key);
                else psi.Environment[key] = value;
            }

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            try
            {
                if (stdin is not null) process.StandardInput.Write(stdin);
                process.StandardInput.Close();
            }
            catch (IOException) { } // The CLI exited before reading stdin

            if (!process.WaitForExit(timeout ?? TimeSpan.FromSeconds(60)))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Assert.Fail($"CLI timed out: {string.Join(' ', args)}\n{new CliResult(-1, stdout.Result, stderr.Result)}");
            }

            return new CliResult(process.ExitCode, stdout.Result, stderr.Result);
        }

        /// <summary>Asserts a successful (exit code 0) run and returns it.</summary>
        public static CliResult RunOk(string workDir, params string[] args)
        {
            var result = Run(workDir, args);
            Assert.True(result.ExitCode == 0, result.ToString());
            return result;
        }
    }

    /// <summary>Thin wrapper around ffprobe/ffmpeg for asserting on media files produced by the CLI.</summary>
    public static class Media
    {
        public sealed record Probe(double DurationSeconds, string? VideoCodec, int Width, int Height, double Fps, bool HasAudio);

        public static Probe Inspect(string file)
        {
            var ffprobe = Path.Combine(Path.GetDirectoryName(TestData.RequireFfmpeg())!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
            Assert.SkipUnless(File.Exists(ffprobe), "ffprobe is not installed next to ffmpeg");

            var psi = new ProcessStartInfo(ffprobe) { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-v", "error", "-show_entries", "format=duration:stream=codec_type,codec_name,width,height,r_frame_rate", "-of", "json", file })
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)!;
            var stderr = process.StandardError.ReadToEndAsync();
            var json = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"ffprobe failed on {file}:\n{stderr.Result}");

            using var doc = JsonDocument.Parse(json);
            var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            var video = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");
            var fps = 0.0;
            if (video.ValueKind != JsonValueKind.Undefined && video.GetProperty("r_frame_rate").GetString()!.Split('/') is [var num, var den] && double.Parse(den) != 0)
                fps = double.Parse(num) / double.Parse(den);

            return new Probe(
                double.Parse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                video.ValueKind == JsonValueKind.Undefined ? null : video.GetProperty("codec_name").GetString(),
                video.ValueKind == JsonValueKind.Undefined ? 0 : video.GetProperty("width").GetInt32(),
                video.ValueKind == JsonValueKind.Undefined ? 0 : video.GetProperty("height").GetInt32(),
                fps,
                streams.Any(s => s.GetProperty("codec_type").GetString() == "audio"));
        }

        /// <summary>Decodes the frame at <paramref name="seconds"/> into packed RGB24 bytes.</summary>
        public static byte[] FrameRgb(string file, double seconds, string scratchDir)
        {
            var raw = Path.Combine(scratchDir, $"frame-{Guid.NewGuid():N}.rgb");
            TestData.RunFfmpeg("-v", "error", "-ss", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", file, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", raw);
            return File.ReadAllBytes(raw);
        }
    }
}
