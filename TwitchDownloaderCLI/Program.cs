using CommandLine;
using Mono.Unix;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using Xabe.FFmpeg.Downloader;

namespace TwitchDownloaderCLI
{
    class Program
    {
        static string previousStatus = "";
        static string ffmpegPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        static bool was_last_message_percent = false;
        static void Main(string[] args)
        {
            if (args.Any(x => x.Equals("--download-ffmpeg")))
            {
                Console.WriteLine("[INFO] - Downloading ffmpeg and exiting");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).Wait();
                    try
                    {
                        var filePermissions = new Mono.Unix.UnixFileInfo("ffmpeg");
                        filePermissions.FileAccessPermissions = FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite| FileAccessPermissions.GroupRead| FileAccessPermissions.OtherRead | FileAccessPermissions.UserExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.OtherExecute;
                        filePermissions.Refresh();
                    }
                    catch { }
                }
                else
                    FFmpegDownloader.GetLatestVersion(FFmpegVersion.Full).Wait();
                Environment.Exit(1);
            }

            Options inputOptions = new Options();
            var optionsResult = Parser.Default.ParseArguments<Options>(args).WithParsed(r => { inputOptions = r; });

            if (optionsResult.Tag == ParserResultType.NotParsed)
                Environment.Exit(1);

            
            if (!File.Exists(ffmpegPath) && (inputOptions.FfmpegPath == null || !File.Exists(inputOptions.FfmpegPath)) && !ExistsOnPath(ffmpegPath))
            {
                Console.WriteLine("[ERROR] - Unable to find ffmpeg, exiting. You can download ffmpeg automatically with the argument --download-ffmpeg");
                Environment.Exit(1);
            }

            switch (inputOptions.RunMode)
            {
                case RunMode.VideoDownload:
                    DownloadVideo(inputOptions);
                    break;
                case RunMode.ClipDownload:
                    DownloadClip(inputOptions);
                    break;
                case RunMode.ChatDownload:
                    DownloadChat(inputOptions);
                    break;
                case RunMode.ChatRender:
                    RenderChat(inputOptions);
                    break;
            }
        }

        private static void DownloadVideo(Options inputOptions)
        {
            VideoDownloadOptions downloadOptions = new VideoDownloadOptions();

            if (inputOptions.Id == "" || !inputOptions.Id.All(Char.IsDigit))
            {
                Console.WriteLine("[ERROR] - Invalid VOD ID, unable to parse. Must be only numbers.");
                Environment.Exit(1);
            }

            downloadOptions.DownloadThreads = inputOptions.DownloadThreads;
            downloadOptions.Id = Int32.Parse(inputOptions.Id);
            downloadOptions.Oauth = inputOptions.Oauth;
            downloadOptions.Filename = inputOptions.OutputFile;
            downloadOptions.Quality = inputOptions.Quality;
            downloadOptions.CropBeginning = inputOptions.CropBeginningTime == 0.0 ? false : true;
            downloadOptions.CropBeginningTime = inputOptions.CropBeginningTime;
            downloadOptions.CropEnding = inputOptions.CropEndingTime == 0.0 ? false : true;
            downloadOptions.CropEndingTime = inputOptions.CropEndingTime;
            downloadOptions.FfmpegPath = inputOptions.FfmpegPath == null || inputOptions.FfmpegPath == "" ? ffmpegPath : Path.GetFullPath(inputOptions.FfmpegPath);
            downloadOptions.TempFolder = inputOptions.TempFolder;

            VideoDownloader videoDownloader = new VideoDownloader(downloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            videoDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }

        //https://stackoverflow.com/a/3856090/12204538
        public static bool ExistsOnPath(string fileName)
        {
            return GetFullPath(fileName) != null;
        }

        public static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        private static void DownloadClip(Options inputOptions)
        {
            ClipDownloadOptions downloadOptions = new ClipDownloadOptions();

            if (inputOptions.Id == "" || inputOptions.Id.All(Char.IsDigit))
            {
                Console.WriteLine("[ERROR] - Invalid Clip ID, unable to parse.");
                Environment.Exit(1);
            }

            downloadOptions.Id = inputOptions.Id;
            downloadOptions.Filename = inputOptions.OutputFile;
            downloadOptions.Quality = inputOptions.Quality;

            ClipDownloader clipDownloader = new ClipDownloader(downloadOptions);
            clipDownloader.DownloadAsync().Wait();
        }

        private static void DownloadChat(Options inputOptions)
        {
            ChatDownloadOptions downloadOptions = new ChatDownloadOptions();

            if (inputOptions.Id == "")
            {
                Console.WriteLine("[ERROR] - Invalid ID, unable to parse.");
                Environment.Exit(1);
            }
            
            //If output file doesn't end in .txt, assume JSON
            if (Path.GetFileName(inputOptions.OutputFile).Contains('.'))
            {
                string extension = Path.GetFileName(inputOptions.OutputFile).Split('.').Last();
                if (extension.ToLower() == "json")
                    downloadOptions.IsJson = true;
                else
                    downloadOptions.IsJson = false;
            }
            else
            {
                downloadOptions.IsJson = true;
            }

            downloadOptions.Id = inputOptions.Id;
            downloadOptions.CropBeginning = inputOptions.CropBeginningTime == 0.0 ? false : true;
            downloadOptions.CropBeginningTime = inputOptions.CropBeginningTime;
            downloadOptions.CropEnding = inputOptions.CropEndingTime == 0.0 ? false : true;
            downloadOptions.CropEndingTime = inputOptions.CropEndingTime;
            downloadOptions.Timestamp = inputOptions.Timestamp;
            downloadOptions.EmbedEmotes = inputOptions.EmbedEmotes;
            downloadOptions.Filename = inputOptions.OutputFile;

            ChatDownloader chatDownloader = new ChatDownloader(downloadOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            chatDownloader.DownloadAsync(progress, new CancellationToken()).Wait();
        }

        private static void RenderChat(Options inputOptions)
        {
            ChatRenderOptions renderOptions = new ChatRenderOptions();

            renderOptions.InputFile = inputOptions.InputFile;
            renderOptions.OutputFile = inputOptions.OutputFile;
            renderOptions.BackgroundColor = SKColor.Parse(inputOptions.BackgroundColor);
            renderOptions.MessageColor = SKColor.Parse(inputOptions.MessageColor);
            renderOptions.ChatHeight = inputOptions.ChatHeight;
            renderOptions.ChatWidth = inputOptions.ChatWidth;
            renderOptions.BttvEmotes = inputOptions.BttvEmotes;
            renderOptions.FfzEmotes = inputOptions.FfzEmotes;
            renderOptions.Outline = inputOptions.Outline;
            renderOptions.OutlineSize = inputOptions.OutlineSize;
            renderOptions.Font = inputOptions.Font;
            renderOptions.FontSize = inputOptions.FontSize;

            switch (inputOptions.MessageFontStyle.ToLower())
            {
                case "normal":
                    renderOptions.MessageFontStyle = SKFontStyle.Normal;
                    break;
                case "bold":
                    renderOptions.MessageFontStyle = SKFontStyle.Bold;
                    break;
                case "italic":
                    renderOptions.MessageFontStyle = SKFontStyle.Italic;
                    break;
            }

            switch (inputOptions.UsernameFontStyle.ToLower())
            {
                case "normal":
                    renderOptions.UsernameFontStyle = SKFontStyle.Normal;
                    break;
                case "bold":
                    renderOptions.UsernameFontStyle = SKFontStyle.Bold;
                    break;
                case "italic":
                    renderOptions.UsernameFontStyle = SKFontStyle.Italic;
                    break;
            }

            renderOptions.UpdateRate = inputOptions.UpdateRate;
            renderOptions.PaddingLeft = inputOptions.PaddingLeft;
            renderOptions.Framerate = inputOptions.Framerate;
            renderOptions.GenerateMask = inputOptions.GenerateMask;
            renderOptions.InputArgs = inputOptions.InputArgs;
            renderOptions.OutputArgs = inputOptions.OutputArgs;
            renderOptions.FfmpegPath = inputOptions.FfmpegPath == null || inputOptions.FfmpegPath == "" ? ffmpegPath : Path.GetFullPath(inputOptions.FfmpegPath);
            renderOptions.TempFolder = inputOptions.TempFolder;
            renderOptions.SubMessages = inputOptions.SubMessages;

            if (renderOptions.GenerateMask && renderOptions.BackgroundColor.Alpha == 255)
            {
                Console.WriteLine("[WARNING] - Generate mask option has been selected with an opaque background. You most likely want to set a transparent background with --background-color \"#00000000\"");
            }

            ChatRenderer chatDownloader = new ChatRenderer(renderOptions);
            Progress<ProgressReport> progress = new Progress<ProgressReport>();
            progress.ProgressChanged += Progress_ProgressChanged;
            chatDownloader.RenderVideoAsync(progress, new CancellationToken()).Wait();
        }

        private static void Progress_ProgressChanged(object sender, ProgressReport e)
        {
            if (e.reportType == ReportType.Message)
            {
                if (was_last_message_percent)
                {
                    was_last_message_percent = false;
                    Console.WriteLine("");
                }
                string currentStatus = "[STATUS] - " + e.data;
                if (currentStatus != previousStatus)
                {
                    previousStatus = currentStatus;
                    Console.WriteLine(currentStatus);
                }
            }
            else if (e.reportType == ReportType.Log)
            {
                if(was_last_message_percent)
                {
                    was_last_message_percent = false;
                    Console.WriteLine("");
                }
                Console.WriteLine("[LOG] - " + e.data);
            }
            else if (e.reportType == ReportType.MessageInfo)
            {
                Console.Write("\r[STATUS] - " + e.data);
                was_last_message_percent = true;
            }
        }
    }
}
