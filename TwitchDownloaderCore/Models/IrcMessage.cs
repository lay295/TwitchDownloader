using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TwitchDownloaderCore.Models
{
    public sealed record IrcMessage
    {
        public IrcCommand Command { get; init; }

        /// <remarks>
        /// If neither a user nor host are present, this field becomes the server name
        /// </remarks>>
        public string Nickname { get; init; }

        public string User { get; init; }

        public string Host { get; init; }

        public string ParametersRaw { get; init; }

        [AllowNull]
        public Dictionary<string, string> Tags { get; init; }

        public bool TryGetTag(string name, out string value)
        {
            if (Tags is null)
            {
                value = null;
                return false;
            }

            return Tags.TryGetValue(name, out value);
        }
    }
}