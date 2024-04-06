using System;
using System.Collections.Generic;
using System.Linq;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Tools
{
    internal static class PreParseArgs
    {
        internal static string[] Parse(string[] args, string processFileName)
        {
            if (args.Any(x => x is "-m" or "--mode" or "--embed-emotes" or "--silent" or "--verbose-ffmpeg"))
            {
                // A legacy syntax was used, convert to new syntax
                return Process(ConvertFromOldSyntax(args, processFileName));
            }

            return Process(args);
        }

        private static string[] Process(string[] args)
        {
            if (args.Length > 0)
            {
                args[0] = args[0].ToLower();
            }

            return args;
        }

        /// <summary>
        /// Converts an argument <see cref="string"/>[] using any legacy syntax to the current syntax and prints corresponding warning messages
        /// </summary>
        /// <returns>An argument <see cref="string"/>[] using current syntax that represent the intentions of the legacy syntax</returns>
        private static string[] ConvertFromOldSyntax(string[] args, string processFileName)
        {
            var processedArgs = args.ToList();

            for (var i = 0; i < processedArgs.Count; i++)
            {
                switch (processedArgs[i])
                {
                    case "--embed-emotes":
                        Console.WriteLine("[INFO] The program has switched from --embed-emotes to -E / --embed-images, consider using those instead. Run \'{0} help\' for more information.", processFileName);
                        ConvertEmbedEmoteSyntax(processedArgs, i);
                        break;
                    case "-m" or "--mode":
                        Console.WriteLine("[INFO] The program has switched from --mode <mode> to verbs (like \'git <verb>\'), consider using verbs instead. Run \'{0} help\' for more information.", processFileName);
                        ConvertModeSyntax(processedArgs, i);
                        break;
                    case "--silent":
                        Console.WriteLine("[INFO] The program has switched from --silent to log levels, consider using log levels instead. '--log-level None' will be applied to the current session. Run \'{0} help\' for more information.", processFileName);
                        ConvertSilentSyntax(processedArgs, i);
                        break;
                    case "--verbose-ffmpeg":
                        Console.WriteLine("[INFO] The program has switched from --verbose-ffmpeg to log levels, consider using log levels instead. '--log-level Status,Info,Warning,Error,Ffmpeg' will be applied to the current session. Run \'{0} help\' for more information.", processFileName);
                        ConvertVerboseFfmpegSyntax(processedArgs, i);
                        break;
                }
            }

            return processedArgs.ToArray();
        }

        private static void ConvertEmbedEmoteSyntax(IList<string> args, int index)
        {
            args[index] = "-E";
        }

        private static void ConvertModeSyntax(IList<string> args, int index)
        {
            // --mode
            args.RemoveAt(index);

            // run-mode
            var runMode = args[index];
            args.RemoveAt(index);
            args.Insert(0, runMode);
        }

        private static void ConvertSilentSyntax(IList<string> args, int index)
        {
            args[index] = "--log-level";
            args.Insert(index + 1, nameof(LogLevel.None));
        }

        private static void ConvertVerboseFfmpegSyntax(IList<string> args, int index)
        {
            // If the user is still using --verbose-ffmpeg they probably aren't using log levels yet, so its safe to assume that there won't be a double-parse error
            args[index] = "--log-level";

            var logLevels = Enum.GetNames(typeof(LogLevel))
                .Where(x => x is not nameof(LogLevel.None) and not nameof(LogLevel.Verbose));

            args.Insert(index + 1, string.Join(',', logLevels));
        }
    }
}
