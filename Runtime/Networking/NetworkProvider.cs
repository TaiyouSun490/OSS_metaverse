using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public abstract class NetworkProvider : ScriptableObject
    {
        public event Action<ConnectionState> StateChanged;
        public event Action<PeerInfo> PeerJoined;
        public event Action<PeerInfo> PeerLeft;
        public event Action<NetworkMessage> MessageReceived;
        public event Action<Exception> Error;

        public abstract ConnectionState State { get; }
        public abstract PeerId LocalPeer { get; }
        public abstract Task InitializeAsync(CancellationToken cancellationToken);
        public abstract Task JoinAsync(RoomRequest request, CancellationToken cancellationToken);
        public abstract Task LeaveAsync(CancellationToken cancellationToken);
        public abstract void Send(byte channel, ArraySegment<byte> payload, DeliveryMode delivery);

        public virtual Task<IReadOnlyList<RoomInfo>> DiscoverPublicRoomsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RoomInfo>>(Array.Empty<RoomInfo>());
        }

        public virtual void Tick(float unscaledDeltaTime) { }

        protected void RaiseStateChanged(ConnectionState state) => StateChanged?.Invoke(state);
        protected void RaisePeerJoined(PeerInfo peer) => PeerJoined?.Invoke(peer);
        protected void RaisePeerLeft(PeerInfo peer) => PeerLeft?.Invoke(peer);
        protected void RaiseMessage(NetworkMessage message) => MessageReceived?.Invoke(message);
        protected void RaiseError(Exception exception) => Error?.Invoke(exception);
    }
}
