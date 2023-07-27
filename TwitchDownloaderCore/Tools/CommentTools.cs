using System.Collections.Generic;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    internal class SortedCommentComparer : IComparer<Comment>
    {
        // Modified from double.CompareTo
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        public int Compare(Comment x, Comment y)
        {
            if (x is null)
            {
                if (y is null) return 0;
                return -1;
            }

            if (y is null) return -1;

            // In the off chance that it causes problems with old chats, we will first compare offsets before comparing creation dates.
            var xOffset = x.content_offset_seconds;
            var yOffset = y.content_offset_seconds;

            if (xOffset < yOffset) return -1;
            if (xOffset > yOffset) return 1;
            if (xOffset == yOffset)
            {
                // Offset seconds are equal, compare the creation dates
                var xCreatedAt = x.created_at;
                var yCreatedAt = y.created_at;

                if (xCreatedAt < yCreatedAt) return -1;
                if (xCreatedAt > yCreatedAt) return 1;
                if (xCreatedAt == yCreatedAt) return 1; // Returning 0 would result in y being discarded by the sorter
            }

            // At least one of the values is NaN.
            if (double.IsNaN(xOffset))
                return double.IsNaN(yOffset) ? 0 : -1;

            return 1;
        }
    }
}