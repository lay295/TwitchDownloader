using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Models.Render
{
    // Keeps decoded emote and GIF frames within a budget. Frames are the expensive part of an image and can be
    // decoded again on demand, so the least recently drawn are released once too many are held at once.
    public sealed class ImageMemoryBudget
    {
        private readonly long _budgetBytes;
        private long _drawClock;

        public ImageMemoryBudget(long budgetBytes) => _budgetBytes = Math.Max(1, budgetBytes);

        /// <summary>Stamps <paramref name="images"/> as just drawn, ordering them for later release.</summary>
        public void MarkDrawn(IEnumerable<TwitchEmote> images)
        {
            foreach (var image in images)
            {
                image.LastUsedTick = ++_drawClock;
            }
        }

        /// <summary>
        /// Releases the frames of decoded images that are not <paramref name="onScreen"/>, least recently drawn first,
        /// while more than the budget is held. Returns whether anything was released.
        /// </summary>
        /// <remarks>
        /// On screen images are excluded from the total, not merely protected: they have to be decoded to be drawn, so
        /// counting them would let a few large GIFs force out every off screen emote only to re-decode them next frame.
        /// </remarks>
        public bool ReleaseUnused(IEnumerable<TwitchEmote> decoded, HashSet<TwitchEmote> onScreen)
        {
            var retainedBytes = 0L;
            var releasable = new List<TwitchEmote>();

            foreach (var image in decoded)
            {
                if (onScreen.Contains(image))
                {
                    continue;
                }

                retainedBytes += image.DecodedByteSize;
                releasable.Add(image);
            }

            if (retainedBytes <= _budgetBytes)
            {
                return false;
            }

            releasable.Sort(static (a, b) => a.LastUsedTick.CompareTo(b.LastUsedTick));

            var released = false;
            foreach (var image in releasable)
            {
                if (retainedBytes <= _budgetBytes)
                {
                    break;
                }

                retainedBytes -= image.DecodedByteSize;
                image.ReleaseFrames();
                released = true;
            }

            return released;
        }
    }
}
