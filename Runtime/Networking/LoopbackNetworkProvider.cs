using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Networking/Loopback Provider", fileName = "LoopbackNetworkProvider")]
    public sealed class LoopbackNetworkProvider : NetworkProvider
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, LoopbackRoom> Rooms = new Dictionary<string, LoopbackRoom>();

        private readonly Queue<Action> dispatchQueue = new Queue<Action>();
        private ConnectionState state;
        private PeerId localPeer;
        private string currentRoom;

        public override ConnectionState State => state;
        public override PeerId LocalPeer => localPeer;

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(ConnectionState.Initializing);
            localPeer = new PeerId(Guid.NewGuid().ToString("N"));
            SetState(ConnectionState.Ready);
            return Task.CompletedTask;
        }

        public override Task JoinAsync(RoomRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state != ConnectionState.Ready)
                throw new InvalidOperationException($"Cannot join while provider is {state}.");

            var normalized = request ?? new RoomRequest();
            SetState(ConnectionState.Joining);
            currentRoom = string.IsNullOrWhiteSpace(normalized.roomId) ? "lobby" : normalized.roomId.Trim();

            try
            {
                lock (Gate)
                {
                    if (!Rooms.TryGetValue(currentRoom, out var room))
                    {
                        if (!normalized.createIfMissing)
                            throw new InvalidOperationException($"Room '{currentRoom}' does not exist.");
                        if (normalized.visibility == RoomVisibility.Private && string.IsNullOrWhiteSpace(normalized.accessToken))
                            throw new UnauthorizedAccessException("A private room requires an access token.");
                        room = new LoopbackRoom(currentRoom, normalized.visibility, normalized.capacity, normalized.accessToken);
                        Rooms.Add(currentRoom, room);
                    }

                    if (room.Visibility == RoomVisibility.Private &&
                        !string.Equals(room.AccessToken, normalized.accessToken, StringComparison.Ordinal))
                        throw new UnauthorizedAccessException($"Access to private room '{currentRoom}' was denied.");
                    if (room.Members.Count >= room.Capacity)
                        throw new InvalidOperationException($"Room '{currentRoom}' is full.");

                    foreach (var member in room.Members.ToArray())
                    {
                        var existing = member;
                        Enqueue(() => RaisePeerJoined(new PeerInfo(existing.localPeer, false)));
                        existing.Enqueue(() => existing.RaisePeerJoined(new PeerInfo(localPeer, false)));
                    }

                    room.Members.Add(this);
                }
            }
            catch
            {
                currentRoom = null;
                SetState(ConnectionState.Ready);
                throw;
            }

            SetState(ConnectionState.Joined);
            RaisePeerJoined(new PeerInfo(localPeer, true));
            return Task.CompletedTask;
        }

        public override Task LeaveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state != ConnectionState.Joined)
                return Task.CompletedTask;

            SetState(ConnectionState.Leaving);
            lock (Gate)
            {
                if (Rooms.TryGetValue(currentRoom, out var room))
                {
                    room.Members.Remove(this);
                    foreach (var member in room.Members.ToArray())
                        member.Enqueue(() => member.RaisePeerLeft(new PeerInfo(localPeer, false)));
                    if (room.Members.Count == 0)
                        Rooms.Remove(currentRoom);
                }
            }

            currentRoom = null;
            SetState(ConnectionState.Ready);
            return Task.CompletedTask;
        }

        public override void Send(byte channel, ArraySegment<byte> payload, DeliveryMode delivery)
        {
            if (state != ConnectionState.Joined)
                return;

            var copy = payload.Count == 0 ? Array.Empty<byte>() : payload.ToArray();
            lock (Gate)
            {
                if (!Rooms.TryGetValue(currentRoom, out var room))
                    return;

                foreach (var member in room.Members.Where(member => member != this).ToArray())
                {
                    var target = member;
                    target.Enqueue(() => target.RaiseMessage(
                        new NetworkMessage(localPeer, channel, new ArraySegment<byte>(copy))));
                }
            }
        }

        public override Task<IReadOnlyList<RoomInfo>> DiscoverPublicRoomsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Gate)
            {
                IReadOnlyList<RoomInfo> result = Rooms.Values
                    .Where(room => room.Visibility == RoomVisibility.Public)
                    .Select(room => new RoomInfo(room.Id, room.Visibility, room.Members.Count, room.Capacity))
                    .OrderBy(room => room.Id, StringComparer.Ordinal)
                    .ToArray();
                return Task.FromResult(result);
            }
        }


        public override void Tick(float unscaledDeltaTime)
        {
            while (true)
            {
                Action action;
                lock (dispatchQueue)
                {
                    if (dispatchQueue.Count == 0)
                        break;
                    action = dispatchQueue.Dequeue();
                }
                action.Invoke();
            }
        }

        private void Enqueue(Action action)
        {
            lock (dispatchQueue)
                dispatchQueue.Enqueue(action);
        }

        private void SetState(ConnectionState value)
        {
            state = value;
            RaiseStateChanged(value);
        }

        private sealed class LoopbackRoom
        {
            public string Id { get; }
            public RoomVisibility Visibility { get; }
            public int Capacity { get; }
            public string AccessToken { get; }
            public List<LoopbackNetworkProvider> Members { get; } = new List<LoopbackNetworkProvider>();

            public LoopbackRoom(string id, RoomVisibility visibility, int capacity, string accessToken)
            {
                Id = id;
                Visibility = visibility;
                Capacity = Math.Max(1, capacity);
                AccessToken = accessToken ?? string.Empty;
            }
        }
    }
}
