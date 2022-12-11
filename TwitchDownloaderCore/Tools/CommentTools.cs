using System.Collections.Generic;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    internal class SortedCommentComparer : IComparer<Comment>
    {
        // Modified from double.CompareTo
        public int Compare(Comment x, Comment y)
        {
            double m_value = x.content_offset_seconds;
            double value = y.content_offset_seconds;
            if (m_value < value) return -1;
            if (m_value > value) return 1;
            if (m_value == value) return 1;

            // At least one of the values is NaN.
            if (double.IsNaN(m_value))
                return double.IsNaN(value) ? 0 : -1;
            else
                return 1;
        }
    }
}
