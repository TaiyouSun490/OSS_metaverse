#if TAIYO_METAVERSE_NGO
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Taiyo.Metaverse.Netcode
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Networking/Netcode Provider", fileName = "NetcodeNetworkProvider")]
    public sealed class NetcodeNetworkProvider : NetworkProvider
    {
        private const string MessageName = "taiyo.metaverse.packet.v1";
        [SerializeField] private bool shutdownNetworkManagerOnLeave = true;

        private ConnectionState state;
        private NetworkManager manager;

        public override ConnectionState State => state;
        public override PeerId LocalPeer => manager == null
            ? default
            : new PeerId(manager.LocalClientId.ToString());

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(ConnectionState.Initializing);
            manager = NetworkManager.Singleton;
            if (manager == null)
                throw new InvalidOperationException("A NetworkManager must exist before initializing NetcodeNetworkProvider.");

            manager.OnClientConnectedCallback += OnClientConnected;
            manager.OnClientDisconnectCallback += OnClientDisconnected;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
            SetState(ConnectionState.Ready);
            return Task.CompletedTask;
        }

        public override async Task JoinAsync(RoomRequest request, CancellationToken cancellationToken)
        {
            if (state != ConnectionState.Ready)
                throw new InvalidOperationException($"Cannot join while provider is {state}.");
            SetState(ConnectionState.Joining);
            var started = request != null && request.host ? manager.StartHost() : manager.StartClient();
            if (!started)
                throw new InvalidOperationException("Netcode could not start. Check the configured NetworkTransport.");

            await WaitUntilAsync(() => manager.IsListening && (manager.IsServer || manager.IsConnectedClient), cancellationToken);
            SetState(ConnectionState.Joined);
            RaisePeerJoined(new PeerInfo(LocalPeer, true));
        }

        public override Task LeaveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state != ConnectionState.Joined)
                return Task.CompletedTask;
            SetState(ConnectionState.Leaving);
            if (shutdownNetworkManagerOnLeave && manager != null && manager.IsListening)
                manager.Shutdown();
            SetState(ConnectionState.Ready);
            return Task.CompletedTask;
        }

        public override void Send(byte channel, ArraySegment<byte> payload, DeliveryMode delivery)
        {
            if (state != ConnectionState.Joined || payload.Array == null)
                return;
            var ngoDelivery = delivery == DeliveryMode.Reliable
                ? NetworkDelivery.ReliableSequenced
                : NetworkDelivery.Unreliable;

            if (manager.IsServer)
            {
                foreach (var clientId in manager.ConnectedClientsIds)
                {
                    if (clientId != manager.LocalClientId)
                        SendTo(clientId, manager.LocalClientId, channel, payload, ngoDelivery);
                }
            }
            else
            {
                SendTo(NetworkManager.ServerClientId, manager.LocalClientId, channel, payload, ngoDelivery);
            }
        }

        private void OnNamedMessage(ulong transportSender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ulong claimedSender);
            reader.ReadValueSafe(out byte channel);
            var count = reader.Length - reader.Position;
            var bytes = new byte[count];
            reader.ReadBytesSafe(ref bytes, count);
            var sender = manager.IsServer ? transportSender : claimedSender;

            RaiseMessage(new NetworkMessage(new PeerId(sender.ToString()), channel, new ArraySegment<byte>(bytes)));
            if (!manager.IsServer)
                return;

            foreach (var clientId in manager.ConnectedClientsIds)
            {
                if (clientId != transportSender && clientId != manager.LocalClientId)
                    SendTo(clientId, sender, channel, new ArraySegment<byte>(bytes), NetworkDelivery.Unreliable);
            }
        }

        private void SendTo(ulong target, ulong sender, byte channel, ArraySegment<byte> payload, NetworkDelivery delivery)
        {
            using (var writer = new FastBufferWriter(sizeof(ulong) + sizeof(byte) + payload.Count, Allocator.Temp))
            {
                writer.WriteValueSafe(sender);
                writer.WriteValueSafe(channel);
                writer.WriteBytesSafe(payload.Array, payload.Count, payload.Offset);
                manager.CustomMessagingManager.SendNamedMessage(MessageName, target, writer, delivery);
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != manager.LocalClientId)
                RaisePeerJoined(new PeerInfo(new PeerId(clientId.ToString()), false));
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId != manager.LocalClientId)
                RaisePeerLeft(new PeerInfo(new PeerId(clientId.ToString()), false));
        }

        private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
        {
            while (!condition())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        private void SetState(ConnectionState value)
        {
            state = value;
            RaiseStateChanged(value);
        }

        private void OnDisable()
        {
            if (manager == null)
                return;
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (manager.CustomMessagingManager != null)
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
        }
    }
}
#endif
