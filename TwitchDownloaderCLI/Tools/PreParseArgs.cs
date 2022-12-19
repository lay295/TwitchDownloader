using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchDownloaderCLI.Tools
{
    internal static class PreParseArgs
    {
        internal static string[] Parse(string[] args, string processFileName)
        {
            if (args.Any(x => x is "-m" or "--mode" or "--embed-emotes"))
            {
                // A legacy syntax was used, convert to new syntax
                return Process(ConvertFromOldSyntax(args, processFileName));
            }

            return Process(args);
        }

        internal static string[] Process(string[] args)
        {
            args[0] = args[0].ToLower();
            return args;
        }

        /// <summary>
        /// Converts an argument <see cref="string"/>[] using any legacy syntax to the current syntax and prints corresponding warning messages
        /// </summary>
        /// <param name="args"></param>
        /// <returns>An argument <see cref="string"/>[] using current syntaxes that represent the intentions of the legacy syntax</returns>
        internal static string[] ConvertFromOldSyntax(string[] args, string processFileName)
        {
            List<string> processedArgs = args.ToList();

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

            return processedArgs.ToArray();
        }

        internal static List<string> ConvertEmbedEmoteSyntax(List<string> args)
        {
            int argsLength = args.Count;

            for (int i = 0; i < argsLength; i++)
            {
                if (args[i].Equals("--embed-emotes"))
                {
                    args[i] = "-E";
                    break;
                }
            }

            return args;
        }

        internal static List<string> ConvertModeSyntax(List<string> args)
        {
            int argsLength = args.Count;
            string[] processedArgs = new string[argsLength - 1];

            int j = 1;
            for (int i = 0; i < argsLength; i++)
            {
                if (args[i].Equals("-m") || args[i].Equals("--mode"))
                {
                    // Copy the runmode to the verb position
                    processedArgs[0] = args[i + 1];
                    i++;
                    continue;
                }
                processedArgs[j] = args[i];
                j++;
            }

            return processedArgs.ToList();
        }
    }
}
