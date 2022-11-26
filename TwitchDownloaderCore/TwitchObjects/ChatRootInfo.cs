using System;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class ChatRootInfo
    {
        public ChatRootVersion Version { get; set; } = new ChatRootVersion("1.0.0"); // Default to 1.0.0 for older jsons that don't have this field
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ChatRootVersion
    {
        // Fields
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }

        // Constructors
        public ChatRootVersion() { Major = 1; Minor = 0; Patch = 0; }

        public ChatRootVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public ChatRootVersion(string version)
        {
            string[] versionArray = version.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Major = 1;
            Minor = 0;
            Patch = 0;

            if (versionArray.Length == 0)
                return;

            Major = int.Parse(versionArray[0]);

            if (versionArray.Length == 1)
                return;

            Minor = int.Parse(versionArray[1]);

            if (versionArray.Length == 2)
                return;

            Patch = int.Parse(versionArray[2]);
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
        public static bool operator >(ChatRootVersion crvA, ChatRootVersion crvB)
        {
            if (crvA.Major > crvB.Major) return true;
            else if (crvA.Major == crvB.Major)
            {
                if (crvA.Minor > crvB.Minor) return true;
                else if (crvA.Minor == crvB.Minor)
                {
                    if (crvA.Patch > crvB.Patch) return true;
                }
            }
            return false;
        }

        public static bool operator <(ChatRootVersion crvA, ChatRootVersion crvB)
        {
            if (crvA.Major < crvB.Major) return true;
            else if (crvA.Major == crvB.Major)
            {
                if (crvA.Minor < crvB.Minor) return true;
                else if (crvA.Minor == crvB.Minor)
                {
                    if (crvA.Patch < crvB.Patch) return true;
                }
            }
            return false;
        }

        public static bool operator ==(ChatRootVersion crvA, ChatRootVersion crvB)
        {
            if (crvA.Major != crvB.Major) return false;
            if (crvA.Minor != crvB.Minor) return false;
            if (crvA.Patch != crvB.Patch) return false;
            return true;
        }

        public static bool operator !=(ChatRootVersion crvA, ChatRootVersion crvB)
            => !(crvA == crvB);

        public static bool operator >=(ChatRootVersion crvA, ChatRootVersion crvB)
            => (crvA > crvB) || (crvA == crvB);

        public static bool operator <=(ChatRootVersion crvA, ChatRootVersion crvB)
            => (crvA < crvB) || (crvA == crvB);
    }
}
