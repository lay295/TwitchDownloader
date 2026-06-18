using System;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class VideoNodeGame
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class VideoNode
    {
        public string title { get; set; }
        public string id { get; set; }
        public int lengthSeconds { get; set; }
        public string previewThumbnailURL { get; set; }
        public DateTime createdAt { get; set; }
        public int viewCount { get; set; }
        public VideoNodeGame game { get; set; }
    }

    public class VideoEdge
    {
        public VideoNode node { get; set; }
        public string cursor { get; set; }
    }

    public class PageInfo
    {
        public bool hasNextPage { get; set; }
        public bool hasPreviousPage { get; set; }
    }

    public class Videos
    {
        public List<VideoEdge> edges { get; set; }
        public PageInfo pageInfo { get; set; }
        public int totalCount { get; set; }
    }

    public class VideoUser
    {
        public Videos videos { get; set; }
    }

    public class VideoSearchData
    {
        public VideoUser user { get; set; }
    }

    public class Extensions
    {
        public int durationMilliseconds { get; set; }
        public string requestID { get; set; }
    }

    public class GqlVideoSearchResponse
    {
        public VideoSearchData data { get; set; }
        public Extensions extensions { get; set; }
    }
}
