using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchDownloaderCLI.Tools
{
    internal static class PreParseArgs
    {
        internal static string[] Process(string[] args)
        {
            string[] processedArgs = args;
            processedArgs[0] = processedArgs[0].ToLower();
            return processedArgs;
        }

        /// <summary>
        /// Converts an argument array that uses any legacy syntax to the current syntax
        /// </summary>
        /// <param name="args"></param>
        /// <returns>The same <paramref name="args"/> array but using current syntax instead
        internal static string[] ConvertFromOldSyntax(string[] args, string processFileName)
        {
            int argsLength = args.Length;
            List<string> processedArgs = args.ToList();

            if (args.Any(x => x.Equals("--embed-emotes")))
            {
                Console.WriteLine("[INFO] The program has switched from --embed-emotes to --embed-images OR -E, consider using those instead. Run \'{0} help\' for more information.", processFileName);
                for (int i = 0; i < argsLength; i++)
                {
                    if (processedArgs[i].Equals("--embed-emotes"))
                    {
                        processedArgs[i] = "-E";
                        break;
                    }
                }
            }

            // This must always be performed last
            if (args.Any(x => x.Equals("-m") || x.Equals("--mode")))
            {
                Console.WriteLine("[INFO] The program has switched from --mode <mode> to verbs (like \'git <verb>\'), consider using verbs instead. Run \'{0} help\' for more information.", processFileName);
                int j = 1;
                for (int i = 0; i < argsLength; i++)
                {
                    if (processedArgs[i].Equals("-m") || processedArgs[i].Equals("--mode"))
                    {
                        // Copy the runmode to the verb position
                        processedArgs[0] = processedArgs[i + 1];
                        i++;
                        continue;
                    }
                    processedArgs[j] = processedArgs[i];
                    j++;
                }
                // Remove last element as it will be a duplicate of second last element
                processedArgs.RemoveAt(processedArgs.Count - 1);
            }

            return processedArgs.ToArray();
        }
    }
}
