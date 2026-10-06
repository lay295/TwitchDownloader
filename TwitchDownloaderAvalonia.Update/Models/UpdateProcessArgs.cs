namespace TwitchDownloaderAvalonia.Update.Models
{
    public sealed record UpdateProcessArgs(
        int ParentPid,
        string InstallDirectory,
        string LocalVersion,
        bool DryRun = false,
        string? Culture = null)
    {
        public const string PID_FLAG = "--pid";
        public const string INSTALL_DIR_FLAG = "--install-dir";
        public const string LOCAL_VERSION_FLAG = "--local-version";
        public const string DRY_RUN_FLAG = "--debug-dry-run";
        public const string CULTURE_FLAG = "--culture";

        public static UpdateProcessArgs Parse(IReadOnlyList<string> args)
        {
            var pid = 0;
            var installDir = AppContext.BaseDirectory;
            var localVersion = "0.0.0";
            var dryRun = false;
            string? culture = null;

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (arg.Equals(DRY_RUN_FLAG, StringComparison.OrdinalIgnoreCase))
                {
                    dryRun = true;
                    continue;
                }

                var value = i + 1 < args.Count ? args[i + 1] : null;
                if (value is null || value.StartsWith("--", StringComparison.Ordinal))
                    continue;

                if (arg.Equals(PID_FLAG, StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var parsedPid))
                {
                    pid = parsedPid;
                    i++;
                }
                else if (arg.Equals(INSTALL_DIR_FLAG, StringComparison.OrdinalIgnoreCase))
                {
                    installDir = value;
                    i++;
                }
                else if (arg.Equals(LOCAL_VERSION_FLAG, StringComparison.OrdinalIgnoreCase))
                {
                    localVersion = value;
                    i++;
                }
                else if (arg.Equals(CULTURE_FLAG, StringComparison.OrdinalIgnoreCase))
                {
                    culture = value;
                    i++;
                }
            }

            return new UpdateProcessArgs(pid, installDir, localVersion, dryRun, culture);
        }

        public string[] ToArguments()
        {
            var args = new List<string>
            {
                PID_FLAG, ParentPid.ToString(),
                INSTALL_DIR_FLAG, InstallDirectory,
                LOCAL_VERSION_FLAG, LocalVersion,
            };

            if (DryRun)
                args.Add(DRY_RUN_FLAG);

            if (!string.IsNullOrWhiteSpace(Culture))
            {
                args.Add(CULTURE_FLAG);
                args.Add(Culture);
            }

            return [.. args];
        }
    }
}
