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
        /// Converts an argument array that uses the old -m/--mode syntax to verb syntax
        /// </summary>
        /// <param name="args"></param>
        /// <returns>The same <paramref name="args"/> array but using a verb instead of -m</returns>
        internal static string[] ConvertFromOldSyntax(string[] args)
        {
            int argsLength = args.Length;
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

            return processedArgs;
        }
    }
}
