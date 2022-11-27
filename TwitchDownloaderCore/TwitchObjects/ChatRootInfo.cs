using System;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class ChatRootInfo
    {
        public ChatRootVersion Version { get; set; } = new ChatRootVersion();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ChatRootVersion
    {
        // Fields
        public int Major { get; set; } = 1;
        public int Minor { get; set; } = 0;
        public int Patch { get; set; } = 0;

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
