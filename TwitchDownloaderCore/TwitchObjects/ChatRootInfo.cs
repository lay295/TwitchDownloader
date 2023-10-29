using System;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class ChatRootInfo
    {
        public ChatRootVersion Version { get; init; } = new();
        public DateTime CreatedAt { get; init; } = DateTime.FromBinary(0);
        public DateTime UpdatedAt { get; init; } = DateTime.FromBinary(0);
    }

    public record ChatRootVersion
    {
        public uint Major { get; init; }
        public uint Minor { get; init; }
        public uint Patch { get; init; }

        public static ChatRootVersion CurrentVersion { get; } = new(1, 3, 1);

        /// <summary>
        /// Initializes a new <see cref="ChatRootVersion"/> object with the default version of 1.0.0
        /// </summary>
        public ChatRootVersion()
        {
            Major = 1;
            Minor = 0;
            Patch = 0;
        }

        /// <summary>
        /// Initializes a new <see cref="ChatRootVersion"/> object with the version number of <paramref name="major"/>.<paramref name="minor"/>.<paramref name="patch"/>
        /// </summary>
        public ChatRootVersion(uint major, uint minor, uint patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public override string ToString()
            => $"{Major}.{Minor}.{Patch}";

        public override int GetHashCode()
            => ToString().GetHashCode();

        public static bool operator >(ChatRootVersion left, ChatRootVersion right)
        {
            if (left.Major > right.Major) return true;
            if (left.Major < right.Major) return false;

            if (left.Minor > right.Minor) return true;
            if (left.Minor < right.Minor) return false;

            return left.Patch > right.Patch;
        }

        public static bool operator <(ChatRootVersion left, ChatRootVersion right)
            => right > left;

        public static bool operator >=(ChatRootVersion left, ChatRootVersion right)
            => left == right || left > right;

        public static bool operator <=(ChatRootVersion left, ChatRootVersion right)
            => left == right || left < right;
    }
}
