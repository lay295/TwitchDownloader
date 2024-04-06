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
            if (args.Any(x => x is "-m" or "--mode" or "--embed-emotes" or "--silent"))
            {
                // A legacy syntax was used, convert to new syntax
                return Process(ConvertFromOldSyntax(args, processFileName));
            }

            return Process(args);
        }

        private static string[] Process(string[] args)
        {
            if (args.Length == 0)
                return args;

            args[0] = args[0].ToLower();
            return args;
        }

        /// <summary>
        /// Converts an argument <see cref="string"/>[] using any legacy syntax to the current syntax and prints corresponding warning messages
        /// </summary>
        /// <returns>An argument <see cref="string"/>[] using current syntax that represent the intentions of the legacy syntax</returns>
        private static string[] ConvertFromOldSyntax(string[] args, string processFileName)
        {
            var processedArgs = args.ToList();

            if (args.Any(x => x.Equals("--embed-emotes")))
            {
                Console.WriteLine("[INFO] The program has switched from --embed-emotes to -E / --embed-images, consider using those instead. Run \'{0} help\' for more information.", processFileName);
                processedArgs = ConvertEmbedEmoteSyntax(processedArgs);
            }

            if (args.Any(x => x is "-m" or "--mode"))
            {
                Console.WriteLine("[INFO] The program has switched from --mode <mode> to verbs (like \'git <verb>\'), consider using verbs instead. Run \'{0} help\' for more information.", processFileName);
                processedArgs = ConvertModeSyntax(processedArgs);
            }

            if (args.Any(x => x is "--silent"))
            {
                Console.WriteLine("[INFO] The program has switched from --silent to log levels, consider using log levels instead. '--log-level None' will be applied to the current session. Run \'{0} help\' for more information.", processFileName);
                processedArgs = ConvertSilentSyntax(processedArgs);
            }

            if (args.Any(x => x is "--verbose-ffmpeg"))
            {
                Console.WriteLine("[INFO] The program has switched from --verbose-ffmpeg to log levels, consider using log levels instead. '--log-level Status,Info,Warning,Error,Ffmpeg' will be applied to the current session. Run \'{0} help\' for more information.", processFileName);
                processedArgs = ConvertVerboseFfmpegSyntax(processedArgs);
            }

            return processedArgs.ToArray();
        }

        private static List<string> ConvertEmbedEmoteSyntax(List<string> args)
        {
            var argsLength = args.Count;

            for (var i = 0; i < argsLength; i++)
            {
                if (args[i].Equals("--embed-emotes"))
                {
                    args[i] = "-E";
                    break;
                }
            }

            return args;
        }

        private static List<string> ConvertModeSyntax(List<string> args)
        {
            var argsLength = args.Count;
            var processedArgs = new string[argsLength - 1];

            var j = 1;
            for (var i = 0; i < argsLength; i++)
            {
                if (args[i].Equals("-m") || args[i].Equals("--mode"))
                {
                    // Copy the run-mode to the verb position
                    processedArgs[0] = args[i + 1];
                    i++;
                    continue;
                }
                processedArgs[j] = args[i];
                j++;
            }

            return processedArgs.ToList();
        }

        private static List<string> ConvertSilentSyntax(List<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i].Equals("--silent"))
                {
                    args[i] = "--log-level";
                    args.Insert(i + 1, nameof(LogLevel.None));
                }
            }

            return args;
        }

        private static List<string> ConvertVerboseFfmpegSyntax(List<string> args)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i].Equals("--verbose-ffmpeg"))
                {
                    // If the user is still using --verbose-ffmpeg they probably aren't using log levels yet, so its safe to assume that there won't be a double-parse error
                    args[i] = "--log-level";
                    var logLevels = Enum.GetNames(typeof(LogLevel))
                        .Where(x => x is not nameof(LogLevel.None) and not nameof(LogLevel.Verbose));
                    args.Insert(i + 1, string.Join(',', logLevels));
                }
            }

            return args;
        }
    }
}
