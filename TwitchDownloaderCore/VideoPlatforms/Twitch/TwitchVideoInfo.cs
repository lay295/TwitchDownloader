using System;
using System.Linq;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;

namespace TwitchDownloaderCore.VideoPlatforms.Twitch
{
    public class TwitchVideoInfo : IVideoInfo
    {
        public TwitchVideoInfo(GqlVideoResponse gqlVideoResponse, GqlVideoTokenResponse gqlVideoTokenResponse, string videoId)
        {
            GqlVideoResponse = gqlVideoResponse;
            GqlVideoTokenResponse = gqlVideoTokenResponse;
            Id = videoId;
        }

        public GqlVideoResponse GqlVideoResponse { get; }
        public GqlVideoTokenResponse GqlVideoTokenResponse { get; }
        public string Id { get; }
        public string ThumbnailUrl => GqlVideoResponse.data?.video?.thumbnailURLs?.FirstOrDefault();
        public DateTime CreatedAt => GqlVideoResponse.data?.video?.createdAt ?? DateTime.MinValue;
        public string StreamerName => GqlVideoResponse.data?.video?.owner?.displayName;
        public string Title => GqlVideoResponse.data?.video?.title;
        public int Duration => GqlVideoResponse.data?.video?.lengthSeconds ?? 0;
        public int ViewCount => GqlVideoResponse.data?.video?.viewCount ?? 0;
        public string Game => GqlVideoResponse.data?.video?.game?.displayName;
    }
}