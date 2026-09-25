#if TAIYO_METAVERSE_FUSION
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Taiyo.Metaverse.FusionAdapter
{
    public enum FusionTopology
    {
        Shared,
        HostOrClient
    }

    [CreateAssetMenu(menuName = "Taiyo Metaverse/Networking/Photon Fusion 2 Provider", fileName = "FusionNetworkProvider")]
    public sealed class FusionNetworkProvider : NetworkProvider
    {
        // Ordinary Fusion RPCs have a 512-byte limit. Keep headroom for Fusion's RPC envelope.
        public const int MaxUnreliablePacketBytes = 480;
        public const int MaxReliablePacketBytes = 16384;

        [SerializeField] private FusionTopology topology = FusionTopology.Shared;
        [SerializeField] private string runnerObjectName = "Taiyo Metaverse Fusion Runner";

        private readonly HashSet<int> knownPlayers = new HashSet<int>();
        private ConnectionState state;
        private NetworkRunner runner;
        private FusionRunnerCallbacks callbacks;
        private bool leaving;

        public override ConnectionState State => state;
        public NetworkRunner Runner => runner;
        public override PeerId LocalPeer => runner == null || !runner.IsRunning || !runner.IsPlayer
            ? default
            : ToPeerId(runner.LocalPlayer);

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(ConnectionState.Initializing);
            EnsureRunner();
            SetState(ConnectionState.Ready);
            return Task.CompletedTask;
        }

        public override async Task JoinAsync(RoomRequest request, CancellationToken cancellationToken)
        {
            if (state != ConnectionState.Ready)
                throw new InvalidOperationException($"Cannot join while provider is {state}.");

            request = request ?? new RoomRequest();
            EnsureRunner();
            SetState(ConnectionState.Joining);

            var gameMode = topology == FusionTopology.Shared
                ? GameMode.Shared
                : request.host ? GameMode.Host : GameMode.Client;
            var args = new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = string.IsNullOrWhiteSpace(request.roomId) ? "lobby" : request.roomId,
                PlayerCount = Mathf.Clamp(request.capacity, 1, 255),
                IsOpen = true,
                IsVisible = request.visibility == RoomVisibility.Public,
                EnableClientSessionCreation = topology == FusionTopology.Shared && request.createIfMissing,
                ConnectionToken = string.IsNullOrWhiteSpace(request.accessToken)
                    ? null
                    : Encoding.UTF8.GetBytes(request.accessToken),
                StartGameCancellationToken = cancellationToken
            };

            try
            {
                var result = await runner.StartGame(args);
                if (!result.Ok)
                    throw new InvalidOperationException($"Photon Fusion could not join session: {result}");

                knownPlayers.Clear();
                foreach (var player in runner.ActivePlayers)
                    HandlePlayerJoined(player);
                SetState(ConnectionState.Joined);
            }
            catch
            {
                await DisposeRunnerAsync();
                EnsureRunner();
                SetState(ConnectionState.Ready);
                throw;
            }
        }

        public override async Task LeaveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state != ConnectionState.Joined && state != ConnectionState.Joining)
                return;

            SetState(ConnectionState.Leaving);
            leaving = true;
            try
            {
                await DisposeRunnerAsync();
            }
            finally
            {
                leaving = false;
                knownPlayers.Clear();
                EnsureRunner();
                SetState(ConnectionState.Ready);
            }
        }

        public override void Send(byte channel, ArraySegment<byte> payload, DeliveryMode delivery)
        {
            if (state != ConnectionState.Joined || runner == null || payload.Array == null)
                return;
            var maximumPacketBytes = delivery == DeliveryMode.Reliable
                ? MaxReliablePacketBytes
                : MaxUnreliablePacketBytes;
            if (payload.Count + 1 > maximumPacketBytes)
            {
                RaiseError(new ArgumentOutOfRangeException(
                    nameof(payload),
                    payload.Count,
                    $"Fusion {delivery} packets may contain at most {maximumPacketBytes - 1} payload bytes."));
                return;
            }

            var packet = new byte[payload.Count + 1];
            packet[0] = channel;
            Buffer.BlockCopy(payload.Array, payload.Offset, packet, 1, payload.Count);
            if (delivery == DeliveryMode.Reliable)
                FusionPacketRpc.RPC_SendReliable(runner, packet);
            else
                FusionPacketRpc.RPC_SendUnreliable(runner, packet);
        }

        internal void HandlePlayerJoined(PlayerRef player)
        {
            if (!knownPlayers.Add(player.PlayerId))
                return;
            RaisePeerJoined(new PeerInfo(ToPeerId(player), runner != null && player == runner.LocalPlayer));
        }

        internal void HandlePlayerLeft(PlayerRef player)
        {
            if (knownPlayers.Remove(player.PlayerId))
                RaisePeerLeft(new PeerInfo(ToPeerId(player), false));
        }

        internal void HandlePacket(PlayerRef sender, byte[] packet)
        {
            if (packet == null || packet.Length == 0)
                return;
            var payload = new byte[packet.Length - 1];
            Buffer.BlockCopy(packet, 1, payload, 0, payload.Length);
            RaiseMessage(new NetworkMessage(ToPeerId(sender), packet[0], new ArraySegment<byte>(payload)));
        }

        internal void HandleTransportError(Exception exception)
        {
            RaiseError(exception);
        }

        internal void HandleShutdown(ShutdownReason reason)
        {
            if (leaving || state == ConnectionState.Ready)
                return;
            knownPlayers.Clear();
            SetState(ConnectionState.Failed);
            if (reason != ShutdownReason.Ok)
                RaiseError(new InvalidOperationException($"Photon Fusion stopped: {reason}"));
        }

        private void EnsureRunner()
        {
            if (runner != null)
                return;
            var runnerObject = new GameObject(string.IsNullOrWhiteSpace(runnerObjectName)
                ? "Taiyo Metaverse Fusion Runner"
                : runnerObjectName);
            DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<NetworkRunner>();
            callbacks = runnerObject.AddComponent<FusionRunnerCallbacks>();
            callbacks.Bind(this);
            runner.AddCallbacks(callbacks);
        }

        private async Task DisposeRunnerAsync()
        {
            var oldRunner = runner;
            runner = null;
            callbacks = null;
            if (oldRunner == null)
                return;
            oldRunner.RemoveCallbacks(oldRunner.GetComponent<FusionRunnerCallbacks>());
            if (oldRunner.IsRunning || oldRunner.IsStarting)
                await oldRunner.Shutdown(destroyGameObject: false);
            if (oldRunner != null)
                Destroy(oldRunner.gameObject);
        }

        private static PeerId ToPeerId(PlayerRef player) => new PeerId(player.PlayerId.ToString());

        private void SetState(ConnectionState value)
        {
            state = value;
            RaiseStateChanged(value);
        }

        private void OnDisable()
        {
            if (runner == null)
                return;
            _ = runner.Shutdown();
            runner = null;
            callbacks = null;
            knownPlayers.Clear();
        }
    }

    public sealed class FusionPacketRpc : SimulationBehaviour
    {
        [Rpc(Channel = RpcChannel.ReliableLargeData, InvokeLocal = false,
            TickAligned = false, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public static void RPC_SendReliable(NetworkRunner runner, byte[] packet, RpcInfo info = default)
        {
            Dispatch(runner, packet, info);
        }

        [Rpc(Channel = RpcChannel.Unreliable, InvokeLocal = false,
            TickAligned = false, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public static void RPC_SendUnreliable(NetworkRunner runner, byte[] packet, RpcInfo info = default)
        {
            Dispatch(runner, packet, info);
        }

        private static void Dispatch(NetworkRunner runner, byte[] packet, RpcInfo info)
        {
            var callback = runner == null ? null : runner.GetComponent<FusionRunnerCallbacks>();
            callback?.ReceivePacket(info.Source, packet);
        }
    }

    internal sealed class FusionRunnerCallbacks : MonoBehaviour, INetworkRunnerCallbacks
    {
        private FusionNetworkProvider provider;

        internal void Bind(FusionNetworkProvider value) => provider = value;
        internal void ReceivePacket(PlayerRef sender, byte[] packet) => provider?.HandlePacket(sender, packet);

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => provider?.HandlePlayerJoined(player);
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) => provider?.HandlePlayerLeft(player);
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) => provider?.HandleShutdown(shutdownReason);
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) =>
            provider?.HandleTransportError(new InvalidOperationException($"Photon Fusion connection failed: {reason}"));
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) =>
            provider?.HandleTransportError(new InvalidOperationException($"Photon Fusion disconnected: {reason}"));
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) => request.Accept();

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    }
}
#endif
