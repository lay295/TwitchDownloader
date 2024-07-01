using System;

namespace TwitchDownloaderCore.TwitchObjects;

public class ChatRootInfo {
    public ChatRootVersion Version { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.FromBinary(0);
    public DateTime UpdatedAt { get; init; } = DateTime.FromBinary(0);
}

public record ChatRootVersion {

    /// <summary>
    ///     Initializes a new <see cref="ChatRootVersion" /> object with the default version of 1.0.0
    /// </summary>
    public ChatRootVersion() {
        this.Major = 1;
        this.Minor = 0;
        this.Patch = 0;
    }

    /// <summary>
    ///     Initializes a new <see cref="ChatRootVersion" /> object with the version number of <paramref name="major" />.
    ///     <paramref name="minor" />.<paramref name="patch" />
    /// </summary>
    public ChatRootVersion(uint major, uint minor, uint patch) {
        this.Major = major;
        this.Minor = minor;
        this.Patch = patch;
    }

    public uint Major { get; init; }
    public uint Minor { get; init; }
    public uint Patch { get; init; }

    public static ChatRootVersion CurrentVersion { get; } = new(1, 3, 1);

    public override string ToString()
        => $"{this.Major}.{this.Minor}.{this.Patch}";

    public override int GetHashCode()
        => this.ToString().GetHashCode();

    public static bool operator >(ChatRootVersion left, ChatRootVersion right) {
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
