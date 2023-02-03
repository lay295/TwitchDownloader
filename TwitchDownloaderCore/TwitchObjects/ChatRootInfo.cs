using System;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class ChatRootInfo
    {
        public ChatRootVersion Version { get; init; } = new ChatRootVersion();
        public DateTime CreatedAt { get; init; } = DateTime.FromBinary(0);
        public DateTime UpdatedAt { get; init; } = DateTime.FromBinary(0);

        public ChatRootInfo() { }
    }

    public class ChatRootVersion
    {
        // Fields
        public int Major { get; set; } = 1;
        public int Minor { get; set; } = 0;
        public int Patch { get; set; } = 0;

        public static ChatRootVersion CurrentVersion { get; } = new(1, 2, 1);

        // Constructors
        /// <summary>
        /// Initializes a new <see cref="ChatRootVersion"/> object with the default version of 1.0.0
        /// </summary>
        public ChatRootVersion() { }

        /// <summary>
        /// Initializes a new <see cref="ChatRootVersion"/> object with the version number of <paramref name="major"/>.<paramref name="minor"/>.<paramref name="patch"/>
        /// </summary>
        public ChatRootVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        // Methods
        public override string ToString()
            => $"{Major}.{Minor}.{Patch}";

        public override int GetHashCode()
            => ToString().GetHashCode();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0083:Use pattern matching")]
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (!(obj is ChatRootVersion crv))
                return false;

            return this == crv;
        }

        // Operators
        public static bool operator >(ChatRootVersion left, ChatRootVersion right)
        {
            if (left.Major > right.Major) return true;
            else if (left.Major == right.Major)
            {
                if (left.Minor > right.Minor) return true;
                else if (left.Minor == right.Minor)
                {
                    if (left.Patch > right.Patch) return true;
                }
            }
            return false;
        }

        public static bool operator <(ChatRootVersion left, ChatRootVersion right)
        {
            if (left.Major < right.Major) return true;
            else if (left.Major == right.Major)
            {
                if (left.Minor < right.Minor) return true;
                else if (left.Minor == right.Minor)
                {
                    if (left.Patch < right.Patch) return true;
                }
            }
            return false;
        }

        public static bool operator ==(ChatRootVersion left, ChatRootVersion right)
        {
            if (left.Major != right.Major) return false;
            if (left.Minor != right.Minor) return false;
            if (left.Patch != right.Patch) return false;
            return true;
        }

        public static bool operator !=(ChatRootVersion left, ChatRootVersion right)
            => !(left == right);

        public static bool operator >=(ChatRootVersion left, ChatRootVersion right)
            => (left > right) || (left == right);

        public static bool operator <=(ChatRootVersion left, ChatRootVersion right)
            => (left < right) || (left == right);
    }
}
