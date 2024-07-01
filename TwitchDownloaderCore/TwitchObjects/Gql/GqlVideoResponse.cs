using System;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects.Gql;

public class VideoOwner {
    public string id { get; set; }
    public string displayName { get; set; }
}

public class VideoInfo {
    public string title { get; set; }
    public List<string> thumbnailURLs { get; set; }
    public DateTime createdAt { get; set; }
    public int lengthSeconds { get; set; }
    public VideoOwner owner { get; set; }
    public int viewCount { get; set; }
    public Game game { get; set; }

    /// <remarks>
    ///     Some values, such as newlines, are repeated twice for some reason.
    ///     This can be filtered out with: <code>description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd()</code>
    /// </remarks>
    public string description { get; set; }
}

public class VideoData {
    public VideoInfo video { get; set; }
}

public class GqlVideoResponse {
    public VideoData data { get; set; }
    public Extensions extensions { get; set; }
}
