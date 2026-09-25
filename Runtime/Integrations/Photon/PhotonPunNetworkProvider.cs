#if TAIYO_METAVERSE_PHOTON
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Taiyo.Metaverse.Photon
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Networking/Photon PUN Provider", fileName = "PhotonPunNetworkProvider")]
    public sealed class PhotonPunNetworkProvider : NetworkProvider, IOnEventCallback
    {
        private const byte PacketEventCode = 190;
        private readonly HashSet<int> knownActors = new HashSet<int>();
        private ConnectionState state;

        public override ConnectionState State => state;
        public override PeerId LocalPeer => PhotonNetwork.LocalPlayer == null
            ? default
            : new PeerId(PhotonNetwork.LocalPlayer.ActorNumber.ToString());

        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            SetState(ConnectionState.Initializing);
            PhotonNetwork.AddCallbackTarget(this);
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                if (!PhotonNetwork.ConnectUsingSettings())
                    throw new InvalidOperationException("Photon PUN could not start connecting.");
                await WaitUntilAsync(() => PhotonNetwork.IsConnectedAndReady, cancellationToken);
            }
            SetState(ConnectionState.Ready);
        }

        public override async Task JoinAsync(RoomRequest request, CancellationToken cancellationToken)
        {
            if (state != ConnectionState.Ready)
                throw new InvalidOperationException($"Cannot join while provider is {state}.");
            SetState(ConnectionState.Joining);
            var roomName = string.IsNullOrWhiteSpace(request?.roomId) ? "lobby" : request.roomId;
            var options = new RoomOptions
            {
                MaxPlayers = (byte)Mathf.Clamp(request?.capacity ?? 32, 1, 255),
                IsVisible = request == null || request.visibility == RoomVisibility.Public,
                IsOpen = true
            };
            var started = request != null && !request.createIfMissing
                ? PhotonNetwork.JoinRoom(roomName)
                : PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
            if (!started)
                throw new InvalidOperationException("Photon PUN rejected the room operation.");
            await WaitUntilAsync(() => PhotonNetwork.InRoom, cancellationToken);
            knownActors.Clear();
            foreach (var player in PhotonNetwork.PlayerList)
                knownActors.Add(player.ActorNumber);
            SetState(ConnectionState.Joined);
            RaisePeerJoined(new PeerInfo(LocalPeer, true));
        }

        public override async Task LeaveAsync(CancellationToken cancellationToken)
        {
            if (!PhotonNetwork.InRoom)
                return;
            SetState(ConnectionState.Leaving);
            PhotonNetwork.LeaveRoom();
            await WaitUntilAsync(() => !PhotonNetwork.InRoom, cancellationToken);
            knownActors.Clear();
            SetState(ConnectionState.Ready);
        }

        public override void Send(byte channel, ArraySegment<byte> payload, DeliveryMode delivery)
        {
            if (state != ConnectionState.Joined || payload.Array == null)
                return;
            var packet = new byte[payload.Count + 1];
            packet[0] = channel;
            Buffer.BlockCopy(payload.Array, payload.Offset, packet, 1, payload.Count);
            PhotonNetwork.RaiseEvent(
                PacketEventCode,
                packet,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                new SendOptions { Reliability = delivery == DeliveryMode.Reliable });
        }

        public override void Tick(float unscaledDeltaTime)
        {
            if (!PhotonNetwork.InRoom)
                return;
            var current = new HashSet<int>();
            foreach (var player in PhotonNetwork.PlayerList)
            {
                current.Add(player.ActorNumber);
                if (player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber && knownActors.Add(player.ActorNumber))
                    RaisePeerJoined(new PeerInfo(new PeerId(player.ActorNumber.ToString()), false));
            }
            foreach (var actor in new List<int>(knownActors))
            {
                if (!current.Contains(actor))
                {
                    knownActors.Remove(actor);
                    RaisePeerLeft(new PeerInfo(new PeerId(actor.ToString()), false));
                }
            }
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != PacketEventCode || !(photonEvent.CustomData is byte[] packet) || packet.Length == 0)
                return;
            var payload = new byte[packet.Length - 1];
            Buffer.BlockCopy(packet, 1, payload, 0, payload.Length);
            RaiseMessage(new NetworkMessage(
                new PeerId(photonEvent.Sender.ToString()),
                packet[0],
                new ArraySegment<byte>(payload)));
        }

        private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
        {
            while (!condition())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(20, cancellationToken);
            }
        }

        private void SetState(ConnectionState value)
        {
            state = value;
            RaiseStateChanged(value);
        }

        private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);
    }
}
#endif
