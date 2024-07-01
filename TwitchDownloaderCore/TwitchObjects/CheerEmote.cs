using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TwitchDownloaderCore.TwitchObjects;

[DebuggerDisplay("{prefix}")]
public sealed class CheerEmote : IDisposable {
    public bool Disposed { get; private set; }
    public string prefix { get; set; }
    public List<KeyValuePair<int, TwitchEmote>> tierList { get; set; } = new();

    public KeyValuePair<int, TwitchEmote> getTier(int value) {
        var returnPair = this.tierList.First();
        foreach (var tierPair in this.tierList.TakeWhile(tierPair => tierPair.Key <= value))
            returnPair = tierPair;

        return returnPair;
    }

    public void Resize(double newScale) {
        foreach (var t in this.tierList)
            t.Value.Resize(newScale);
    }

    #region ImplementIDisposable

    public void Dispose() { this.Dispose(true); }

    private void Dispose(bool isDisposing) {
        try {
            if (this.Disposed)
                return;

            if (isDisposing)
                foreach (var (_, emote) in this.tierList)
                    emote?.Dispose();
        } finally {
            this.Disposed = true;
        }
    }

    #endregion

}
