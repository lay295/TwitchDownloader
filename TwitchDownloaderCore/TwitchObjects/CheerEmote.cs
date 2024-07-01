using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TwitchDownloaderCore.TwitchObjects
{
    [DebuggerDisplay("{prefix}")]
    public sealed class CheerEmote : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public string prefix { get; set; }
        public List<KeyValuePair<int, TwitchEmote>> tierList { get; set; } = new List<KeyValuePair<int, TwitchEmote>>();

        public KeyValuePair<int, TwitchEmote> getTier(int value)
        {
            var returnPair = tierList.First();
            foreach (var tierPair in this.tierList.TakeWhile(tierPair => tierPair.Key <= value)) {
                returnPair = tierPair;
            }

            return returnPair;
        }

        public void Resize(double newScale) {
            foreach (var t in this.tierList)
                t.Value.Resize(newScale);
        }

#region ImplementIDisposable

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (Disposed)
                {
                    return;
                }

                if (isDisposing)
                {
                    foreach (var (_, emote) in tierList)
                    {
                        emote?.Dispose();
                    }
                }
            }
            finally
            {
                Disposed = true;
            }
        }

#endregion
    }
}
