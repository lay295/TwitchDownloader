using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    public static class CommentListVerification
    {
        [Flags]
        public enum CorruptionStatus
        {
            Undefined = 0,
            NotCorrupt = 1,
            HasCorruption = 2,
            PartiallyRepaired = 4,
        }

        public static CorruptionStatus VerifyIntegrity(this List<Comment> commentsList, out string message)
        {
            message = "";
            CorruptionStatus corruptionStatus = CorruptionStatus.Undefined;

            if (!VerifyCommentOrder(commentsList))
            {
                message += "1 or more comments are misaligned. ";
                corruptionStatus |= CorruptionStatus.HasCorruption;
            }

            if (!VerifyCommentMessage(commentsList))
            {
                message += "Repaired 1 or more comments with corrupt message data. ";
                corruptionStatus |= CorruptionStatus.PartiallyRepaired;
            }

            if (message.Length == 0)
            {
                message = "OK";
            }

            if (corruptionStatus == CorruptionStatus.Undefined)
            {
                corruptionStatus = CorruptionStatus.NotCorrupt;
            }

            return corruptionStatus;
        }

        /// <returns>True if all of the comments in the <paramref name="comments"/> list are not null and ordered by <see cref="Comment.content_offset_seconds"/>.</returns>
        private static bool VerifyCommentOrder(List<Comment> comments)
        {
            var asSpan = CollectionsMarshal.AsSpan(comments);

            if (asSpan[0] is null)
            {
                return false;
            }

            for (int i = 1; i < asSpan.Length; i++)
            {
                if (asSpan[i] is null)
                {
                    return false;
                }
                if (asSpan[i - 1].content_offset_seconds > asSpan[i].content_offset_seconds)
                {
                    return false;
                }
            }

            return true;
        }

        /// <returns>True if none of the comment messages in the <paramref name="comments"/> list were corrupted.</returns>
        private static bool VerifyCommentMessage(List<Comment> comments)
        {
            var commentSpan = CollectionsMarshal.AsSpan(comments);
            bool repairPerformed = false;

            for (int i = 0; i < commentSpan.Length; i++)
            {
                if (commentSpan[i].message.user_color is "")
                {
                    commentSpan[i].message.user_color = null;
                    repairPerformed = true;
                }

                if (commentSpan[i].message.user_notice_params is not null &&
                    commentSpan[i].message.user_notice_params.msg_id is "")
                {
                    commentSpan[i].message.user_notice_params = null;
                    repairPerformed = true;
                }

                repairPerformed |= VerifyCommentFragments(commentSpan, i);

                repairPerformed |= VerifyCommentCommenter(commentSpan, i);
            }

            return !repairPerformed;
        }

        /// <returns>If a repair was performed or not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static bool VerifyCommentFragments(Span<Comment> commentSpan, int i)
        {
            var fragmentSpan = CollectionsMarshal.AsSpan(commentSpan[i].message.fragments);
            bool repairPerformed = false;

            for (int f = 0; f < fragmentSpan.Length; f++)
            {
                // If both are "" then it should really be null
                if (fragmentSpan[f].emoticon?.emoticon_set_id is "" &&
                    fragmentSpan[f].emoticon?.emoticon_id is "")
                {
                    fragmentSpan[f].emoticon = null;
                    repairPerformed = true;
                    continue;
                }

                // Otherwise if just one is "" then set it to null

                if (fragmentSpan[f].emoticon?.emoticon_set_id is "")
                {
                    fragmentSpan[f].emoticon.emoticon_set_id = null;
                    repairPerformed = true;
                    continue;
                }

                if (fragmentSpan[f].emoticon?.emoticon_id is "")
                {
                    fragmentSpan[f].emoticon.emoticon_id = null;
                    repairPerformed = true;
                    continue;
                }
            }

            return repairPerformed;
        }

        /// <returns>If a repair was performed or not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static bool VerifyCommentCommenter(Span<Comment> commentSpan, int i)
        {
            bool repairPerformed = false;

            if (commentSpan[i].commenter.display_name is "")
            {
                commentSpan[i].commenter.display_name = null;
                repairPerformed = true;
            }

            if (commentSpan[i].commenter._id is "")
            {
                commentSpan[i].commenter._id = null;
                repairPerformed = true;
            }

            if (commentSpan[i].commenter.name is "")
            {
                commentSpan[i].commenter.name = null;
                repairPerformed = true;
            }

            if (commentSpan[i].commenter.type is "")
            {
                commentSpan[i].commenter.type = null;
                repairPerformed = true;
            }

            if (commentSpan[i].commenter.bio is "")
            {
                commentSpan[i].commenter.bio = null;
                repairPerformed = true;
            }

            if (commentSpan[i].commenter.logo is "")
            {
                commentSpan[i].commenter.logo = null;
                repairPerformed = true;
            }

            return repairPerformed;
        }
    }
}