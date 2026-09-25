using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class MetaverseRuntime : MonoBehaviour
    {
        [SerializeField] private MetaverseConfig configuration;
        [SerializeField] private TrackedPoseSource localPoseSource;
        [SerializeField] private RemoteContentService contentService;
        [SerializeField] private Transform remotePlayersRoot;
        [SerializeField] private bool joinDefaultRoomOnStart = true;

        private readonly Dictionary<PeerId, RemotePeer> peers = new Dictionary<PeerId, RemotePeer>();
        private readonly SafetyService safety = new SafetyService();
        private CancellationTokenSource lifetime;
        private AccountProvider account;
        private NetworkProvider network;
        private VoiceProvider voice;
        private LocalUserProfile localProfile;
        private float poseTimer;

        public event Action<ConnectionState> ConnectionStateChanged;
        public event Action<PeerId> RemotePeerJoined;
        public event Action<PeerId> RemotePeerLeft;
        public event Action<NetworkMessage> UserMessageReceived;
        public event Action<Exception> Error;

        public ConnectionState State => network != null ? network.State : ConnectionState.Offline;
        public PeerId LocalPeer => network != null ? network.LocalPeer : default;
        public AccountProvider Account => account;
        public AccountProfile CurrentAccount => account != null ? account.CurrentProfile : null;
        public SafetyService Safety => safety;
        public RemoteContentService Content => contentService;
        public IReadOnlyCollection<PeerId> RemotePeers => peers.Keys;
        public bool IsVoiceCapturing => voice != null && voice.IsCapturing;

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
            localProfile = new LocalUserProfile();
            if (contentService == null)
                contentService = GetComponent<RemoteContentService>();
            if (remotePlayersRoot == null)
            {
                var root = new GameObject("Remote Players");
                root.transform.SetParent(transform, false);
                remotePlayersRoot = root.transform;
            }
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync(lifetime.Token);
                if (joinDefaultRoomOnStart)
                    await JoinAsync(configuration.DefaultRoom, lifetime.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { ReportError(exception); }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (configuration == null)
                throw new InvalidOperationException("MetaverseRuntime requires a MetaverseConfig asset.");
            if (configuration.NetworkProvider == null)
                throw new InvalidOperationException("MetaverseConfig requires a NetworkProvider.");
            if (network != null)
                return;

            localProfile.userId = configuration.LocalUser.userId;
            localProfile.displayName = configuration.LocalUser.displayName;
            localProfile.avatarAddress = configuration.LocalUser.avatarAddress;

            if (configuration.AccountProvider != null)
            {
                account = Instantiate(configuration.AccountProvider);
                account.name = configuration.AccountProvider.name + " (Runtime)";
                account.ProfileChanged += OnAccountProfileChanged;
                account.Error += ReportError;
                await account.InitializeAsync(cancellationToken);
            }

            network = Instantiate(configuration.NetworkProvider);
            network.name = configuration.NetworkProvider.name + " (Runtime)";
            network.StateChanged += OnStateChanged;
            network.PeerJoined += OnPeerJoined;
            network.PeerLeft += OnPeerLeft;
            network.MessageReceived += OnNetworkMessage;
            network.Error += ReportError;
            await network.InitializeAsync(cancellationToken);

            if (configuration.VoiceProvider != null)
            {
                voice = Instantiate(configuration.VoiceProvider);
                voice.name = configuration.VoiceProvider.name + " (Runtime)";
                await voice.InitializeAsync(gameObject, network, safety, cancellationToken);
            }
        }

        public async Task JoinAsync(RoomRequest request, CancellationToken cancellationToken = default)
        {
            if (network == null)
                await InitializeAsync(cancellationToken);
            await network.JoinAsync(request ?? configuration.DefaultRoom, cancellationToken);
            BroadcastPresence();
            if (voice != null)
                await voice.StartCaptureAsync(cancellationToken);
        }

        public Task LeaveAsync(CancellationToken cancellationToken = default)
        {
            return network == null ? Task.CompletedTask : network.LeaveAsync(cancellationToken);
        }

        public async Task SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken = default)
        {
            if (account == null)
            {
                if (configuration == null || configuration.AccountProvider == null)
                    throw new InvalidOperationException("MetaverseConfig requires an AccountProvider for sign-in.");
                await InitializeAsync(cancellationToken);
            }
            await account.SignInAsync(request, cancellationToken);
        }

        public async Task RegisterAsync(AccountRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            if (account == null)
            {
                if (configuration == null || configuration.AccountProvider == null)
                    throw new InvalidOperationException("MetaverseConfig requires an AccountProvider for registration.");
                await InitializeAsync(cancellationToken);
            }
            await account.RegisterAsync(request, cancellationToken);
        }

        public void SendUserMessage(byte channel, ArraySegment<byte> payload, DeliveryMode delivery = DeliveryMode.Reliable)
        {
            if (channel < MetaverseChannels.UserStart)
                throw new ArgumentOutOfRangeException(nameof(channel), $"User channels start at {MetaverseChannels.UserStart}.");
            if (payload.Count > configuration.MaximumPayloadBytes)
                throw new ArgumentException($"Payload exceeds {configuration.MaximumPayloadBytes} bytes.", nameof(payload));
            network?.Send(channel, payload, delivery);
        }

        public async Task<IReadOnlyList<RoomInfo>> DiscoverPublicRoomsAsync(CancellationToken cancellationToken = default)
        {
            if (network == null)
                await InitializeAsync(cancellationToken);
            return await network.DiscoverPublicRoomsAsync(cancellationToken);
        }


        public Task LoadWorldAsync(string address, CancellationToken cancellationToken = default)
        {
            if (contentService == null)
                throw new InvalidOperationException("A RemoteContentService is required to load a world.");
            return contentService.LoadWorldAsync(address, cancellationToken);
        }

        private void Update()
        {
            network?.Tick(Time.unscaledDeltaTime);
            voice?.Tick(Time.unscaledDeltaTime);

            if (network == null || network.State != ConnectionState.Joined || localPoseSource == null)
                return;
            poseTimer += Time.unscaledDeltaTime;
            if (poseTimer < 1f / configuration.PoseSendRate)
                return;
            poseTimer = 0f;
            if (localPoseSource.TryGetPose(out var pose))
            {
                var payload = PoseCodec.Encode(pose);
                network.Send(MetaverseChannels.Pose, new ArraySegment<byte>(payload), DeliveryMode.Unreliable);
            }
        }

        private void OnStateChanged(ConnectionState value)
        {
            ConnectionStateChanged?.Invoke(value);
            if (value == ConnectionState.Ready)
                ClearPeers();
        }

        private void OnPeerJoined(PeerInfo peer)
        {
            if (peer.IsLocal || peer.Id == network.LocalPeer)
                return;
            if (!peers.ContainsKey(peer.Id))
            {
                peers.Add(peer.Id, new RemotePeer(peer.Id, remotePlayersRoot, configuration.RemotePoseSmoothing, contentService, lifetime.Token));
                RemotePeerJoined?.Invoke(peer.Id);
            }
            BroadcastPresence();
        }

        private void OnPeerLeft(PeerInfo peer)
        {
            if (!peers.TryGetValue(peer.Id, out var remote))
                return;
            remote.Dispose();
            peers.Remove(peer.Id);
            RemotePeerLeft?.Invoke(peer.Id);
        }

        private void OnNetworkMessage(NetworkMessage message)
        {
            if (safety.IsBlocked(message.Sender))
                return;
            if (!peers.TryGetValue(message.Sender, out var remote) && message.Sender != network.LocalPeer)
            {
                remote = new RemotePeer(message.Sender, remotePlayersRoot, configuration.RemotePoseSmoothing, contentService, lifetime.Token);
                peers.Add(message.Sender, remote);
                RemotePeerJoined?.Invoke(message.Sender);
            }

            switch (message.Channel)
            {
                case MetaverseChannels.Pose:
                    if (remote != null && PoseCodec.TryDecode(message.Payload, out var pose))
                        remote.ApplyPose(pose);
                    break;
                case MetaverseChannels.Presence:
                    if (remote != null)
                        remote.ApplyProfile(DecodeProfile(message.Payload));
                    break;
                default:
                    if (message.Channel >= MetaverseChannels.UserStart)
                        UserMessageReceived?.Invoke(message);
                    break;
            }
        }

        private void BroadcastPresence()
        {
            if (network == null || network.State != ConnectionState.Joined)
                return;
            var json = JsonUtility.ToJson(localProfile);
            var bytes = Encoding.UTF8.GetBytes(json);
            network.Send(MetaverseChannels.Presence, new ArraySegment<byte>(bytes), DeliveryMode.Reliable);
        }

        private static LocalUserProfile DecodeProfile(ArraySegment<byte> payload)
        {
            if (payload.Array == null || payload.Count == 0)
                return null;
            try
            {
                var json = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
                return JsonUtility.FromJson<LocalUserProfile>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ReportError(Exception exception)
        {
            Debug.LogException(exception, this);
            Error?.Invoke(exception);
        }

        private void OnAccountProfileChanged(AccountProfile value)
        {
            if (value == null)
                return;
            localProfile.userId = value.accountId;
            if (!string.IsNullOrWhiteSpace(value.displayName))
                localProfile.displayName = value.displayName;
            BroadcastPresence();
        }

        private void ClearPeers()
        {
            foreach (var remote in peers.Values)
                remote.Dispose();
            peers.Clear();
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            voice?.Shutdown();
            ClearPeers();
            if (account != null)
            {
                account.ProfileChanged -= OnAccountProfileChanged;
                account.Error -= ReportError;
                Destroy(account);
            }
            if (network != null)
            {
                network.StateChanged -= OnStateChanged;
                network.PeerJoined -= OnPeerJoined;
                network.PeerLeft -= OnPeerLeft;
                network.MessageReceived -= OnNetworkMessage;
                network.Error -= ReportError;
                Destroy(network);
            }
            if (voice != null)
                Destroy(voice);
            lifetime?.Dispose();
        }

        private sealed class RemotePeer : IDisposable
        {
            private readonly CancellationTokenSource cancellation;
            private readonly RemoteContentService content;
            private readonly GameObject container;
            private readonly float smoothing;
            private AvatarRig rig;
            private GameObject avatarInstance;
            private string avatarAddress;

            public RemotePeer(PeerId id, Transform parent, float poseSmoothing, RemoteContentService contentService, CancellationToken lifetimeToken)
            {
                content = contentService;
                smoothing = poseSmoothing;
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
                container = new GameObject($"Peer-{id}");
                container.transform.SetParent(parent, false);
                rig = container.AddComponent<AvatarRig>();
                rig.SetSmoothing(smoothing);
            }

            public void ApplyPose(TrackedPose pose) => rig.SetTargetPose(pose);

            public async void ApplyProfile(LocalUserProfile profile)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.avatarAddress) ||
                    string.Equals(profile.avatarAddress, avatarAddress, StringComparison.Ordinal))
                    return;
                avatarAddress = profile.avatarAddress;
                if (content == null)
                    return;

                try
                {
                    var loaded = await content.InstantiateAvatarAsync(avatarAddress, container.transform, cancellation.Token);
                    if (cancellation.IsCancellationRequested)
                    {
                        content.ReleaseInstance(loaded);
                        return;
                    }
                    if (avatarInstance != null)
                        content.ReleaseInstance(avatarInstance);
                    avatarInstance = loaded;
                    var loadedRig = avatarInstance.GetComponentInChildren<AvatarRig>();
                    rig.enabled = false;
                    rig = loadedRig != null ? loadedRig : avatarInstance.AddComponent<AvatarRig>();
                    var motionPipeline = avatarInstance.GetComponentInChildren<AvatarMotionPipeline>(true);
                    if (motionPipeline != null)
                        motionPipeline.SetLocallyControlled(false);
                    rig.SetSmoothing(smoothing);
                }
                catch (OperationCanceledException) { }
                catch (Exception exception) { Debug.LogException(exception); }
            }

            public void Dispose()
            {
                cancellation.Cancel();
                if (avatarInstance != null && content != null)
                    content.ReleaseInstance(avatarInstance);
                if (container != null)
                    Destroy(container);
                cancellation.Dispose();
            }
        }
    }
}
