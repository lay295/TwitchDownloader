using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TwitchDownloaderCore.Models
{
    public sealed record IrcMessage
    {
        public IrcCommand Command { get; init; }
    }
}