using System.Collections.Generic;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    public class CommentIdEqualityComparer : IEqualityComparer<Comment>
    {
        public bool Equals(Comment x, Comment y)
        {
            if (x is null) return y is null;
            if (y is null) return false;

            return x._id.Equals(y._id);
        }

        public int GetHashCode(Comment obj) => obj._id.GetHashCode();
    }
}