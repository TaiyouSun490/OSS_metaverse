using System;
using System.Collections.Generic;

namespace Taiyo.Metaverse
{
    public sealed class SafetyService
    {
        private readonly HashSet<PeerId> mutedPeers = new HashSet<PeerId>();
        private readonly HashSet<PeerId> blockedPeers = new HashSet<PeerId>();

        public event Action Changed;

        public bool IsMuted(PeerId peer) => mutedPeers.Contains(peer);
        public bool IsBlocked(PeerId peer) => blockedPeers.Contains(peer);

        public void SetMuted(PeerId peer, bool muted)
        {
            if ((muted ? mutedPeers.Add(peer) : mutedPeers.Remove(peer)))
                Changed?.Invoke();
        }

        public void SetBlocked(PeerId peer, bool blocked)
        {
            var changed = blocked ? blockedPeers.Add(peer) : blockedPeers.Remove(peer);
            if (blocked)
                mutedPeers.Add(peer);
            if (changed)
                Changed?.Invoke();
        }
    }
}
