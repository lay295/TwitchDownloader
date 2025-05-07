using System;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class PortraitCropCoordinates
    {
        public decimal xPercentage { get; set; }
        public decimal yPercentage { get; set; }
    }

    public class PortraitCropMetadata
    {
        public PortraitCropCoordinates topLeft { get; set; }
        public PortraitCropCoordinates bottomRight { get; set; }
    }

    public class StackedTemplateMetadata
    {
        public PortraitCropMetadata topFrame { get; set; }
        public PortraitCropMetadata bottomFrame { get; set; }
    }

    public class FullTemplateMetadata
    {
        public PortraitCropMetadata mainFrame { get; set; }
    }

    public class PortraitMetadata
    {
        public string portraitClipLayout { get; set; }
        public FullTemplateMetadata fullTemplateMetadata { get; set; }
        public StackedTemplateMetadata stackedTemplateMetadata { get; set; }
    }

    public class ShareClipRenderStatusLastBroadcast
    {
        public string id { get; set; }
        public string startedAt { get; set; }
    }

    public class ShareClipRenderStatusStream
    {
        public string id { get; set; }
        public long viewersCount { get; set; }
    }

    public class ShareClipRenderStatusFollowers
    {
        public long totalCount { get; set; }
    }

    public class ShareClipRenderStatusVideo
    {
        public string id { get; set; }
        public string broadcastType { get; set; }
        public string title { get; set; }
    }

    public class ShareClipRenderStatusPlaybackAccessToken
    {
        public string signature { get; set; }
        public string value { get; set; }
    }

    public class ShareClipRenderStatusBroadcaster
    {
        public string id { get; set; }
        public string login { get; set; }
        public string displayName { get; set; }
        public string primaryColorHex { get; set; }
        public bool isPartner { get; set; }
        public string profileImageURL { get; set; }
        public ShareClipRenderStatusFollowers followers { get; set; }
        public ShareClipRenderStatusStream stream { get; set; }
        public ShareClipRenderStatusLastBroadcast lastBroadcast { get; set; }
        public object self { get; set; }
    }

    public class ShareClipRenderStatusBroadcast
    {
        public string id { get; set; }
        public string title { get; set; }
    }

    public class ShareClipRenderStatusGame
    {
        public string id { get; set; }
        public string name { get; set; }
        public string boxArtURL { get; set; }
        public string displayName { get; set; }
        public string slug { get; set; }
    }

    public class ShareClipRenderStatusVideoQuality
    {
        public decimal frameRate { get; set; }
        public string quality { get; set; }
        public string sourceURL { get; set; }
    }

    public class ShareClipRenderStatusCurator
    {
        public string id { get; set; }
        public string login { get; set; }
        public string displayName { get; set; }
        public string profileImageURL { get; set; }
    }

    public class ShareClipRenderStatusAssets
    {
        public string id { get; set; }
        public decimal aspectRatio { get; set; }
        public string type { get; set; }
        public DateTime createdAt { get; set; }
        public string creationState { get; set; }
        public ShareClipRenderStatusCurator curator { get; set; }
        public string thumbnailURL { get; set; }
        public ShareClipRenderStatusVideoQuality[] videoQualities { get; set; }
        public PortraitMetadata portraitMetadata { get; set; }
    }

    public class ShareClipRenderStatusClip
    {
        public string id { get; set; }
        public string slug { get; set; }
        public string url { get; set; }
        public string embedURL { get; set; }
        public string title { get; set; }
        public int viewCount { get; set; }
        public string language { get; set; }
        public bool isFeatured { get; set; }
        public ShareClipRenderStatusAssets[] assets { get; set; }
        public ShareClipRenderStatusCurator curator { get; set; }
        public ShareClipRenderStatusGame game { get; set; }
        public ShareClipRenderStatusBroadcast broadcast { get; set; }
        public ShareClipRenderStatusBroadcaster broadcaster { get; set; }
        public string thumbnailURL { get; set; }
        public DateTime createdAt { get; set; }
        public bool isPublished { get; set; }
        public int durationSeconds { get; set; }
        public ShareClipRenderStatusPlaybackAccessToken playbackAccessToken { get; set; }
        public ShareClipRenderStatusVideo video { get; set; }
        public int? videoOffsetSeconds { get; set; }
        // Duplicate of Assets but with less info. Ignore
        // public ShareClipRenderStatusVideoQualities[] videoQualities { get; set; }
        public bool isViewerEditRestricted { get; set; }
        public object suggestedCropping { get; set; }
    }

    public class ShareClipRenderStatusData
    {
        public ShareClipRenderStatusClip clip { get; set; }
    }

    public class GqlShareClipRenderStatusResponse
    {
        public ShareClipRenderStatusData data { get; set; }
        public Extensions extensions { get; set; }
    }
}