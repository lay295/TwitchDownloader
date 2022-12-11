using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class CommentChannel
    {
        public string id { get; set; }
        public string __typename { get; set; }
    }

    public class CommentCommenter
    {
        public string id { get; set; }
        public string login { get; set; }
        public string displayName { get; set; }
        public string __typename { get; set; }
    }

    public class CommentComments
    {
        public List<CommentEdge> edges { get; set; }
        public CommentPageInfo pageInfo { get; set; }
        public string __typename { get; set; }
    }

    public class Creator
    {
        public string id { get; set; }
        public CommentChannel channel { get; set; }
        public string __typename { get; set; }
    }

    public class CommentData
    {
        public CommentVideo video { get; set; }
    }

    public class CommentEdge
    {
        public string cursor { get; set; }
        public CommentNode node { get; set; }
        public string __typename { get; set; }
    }

    public class CommentFragment
    {
        public CommentEmote emote { get; set; }
        public string text { get; set; }
        public string __typename { get; set; }
    }

    public class CommentEmote
    {
        public string id { get; set; }
        public string emoteID { get; set; }
        public int from { get; set; }
        public string __typename { get; set; }
    }

    public class CommentMessage
    {
        public List<CommentFragment> fragments { get; set; }
        public List<CommentUserBadge> userBadges { get; set; }
        public string userColor { get; set; }
        public string __typename { get; set; }
    }

    public class CommentNode
    {
        public string id { get; set; }
        public CommentCommenter commenter { get; set; }
        public int contentOffsetSeconds { get; set; }
        public DateTime createdAt { get; set; }
        public CommentMessage message { get; set; }
        public string __typename { get; set; }
    }

    public class CommentPageInfo
    {
        public bool hasNextPage { get; set; }
        public bool hasPreviousPage { get; set; }
        public string __typename { get; set; }
    }

    public class GqlCommentResponse
    {
        public CommentData data { get; set; }
        public Extensions extensions { get; set; }
    }

    public class CommentUserBadge
    {
        public string id { get; set; }
        public string setID { get; set; }
        public string version { get; set; }
        public string __typename { get; set; }
    }

    public class CommentVideo
    {
        public string id { get; set; }
        public Creator creator { get; set; }
        public CommentComments comments { get; set; }
        public string __typename { get; set; }
    }
}
