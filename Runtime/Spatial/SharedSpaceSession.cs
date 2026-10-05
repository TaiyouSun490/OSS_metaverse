using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    /// <summary>Opt-in, transport-independent spatial agreement. Call BeginHost/BeginClient after joining.</summary>
    [DisallowMultipleComponent]
    public sealed class SharedSpaceSession : MonoBehaviour
    {
        public MetaverseRuntime runtime;
        public SharedAnchorProvider anchorProvider;
        [Tooltip("Shared virtual origin in this client's world. Used only in SeparateSpaces.")]
        public Transform separateOrigin;
        [Range(64, 255)] public int messageChannel = 80;
        [Min(1)] public float alignmentTimeout = 45;
        [Min(1)] public float peerTimeout = 3;
        public SpaceMode Mode { get; private set; }
        public SpaceState State { get; private set; } = SpaceState.Offline;
        public string Status { get; private set; } = "Not connected";
        public Guid Epoch { get; private set; }
        public bool IsHost { get; private set; }
        public bool IsReady => State == SpaceState.Ready &&
            (Mode == SpaceMode.SeparateSpaces || anchorProvider && anchorProvider.IsLocalized);
        public Pose Frame => Mode == SpaceMode.Colocated && anchorProvider
            ? anchorProvider.AnchorPose : separateOrigin
                ? new Pose(separateOrigin.position, separateOrigin.rotation) : Pose.identity;
        public event Action Changed;
        readonly Dictionary<PeerId, float> readyPeers = new Dictionary<PeerId, float>();
        CancellationTokenSource operation;
        PeerId authority;
        Guid group, anchor;
        float heartbeat;
        bool subscribed;

        public bool IsPeerReady(PeerId peer) => IsReady && readyPeers.TryGetValue(peer, out float time) &&
            Time.unscaledTime - time < peerTimeout;

        void Subscribe()
        {
            if (!runtime) throw new InvalidOperationException("Assign SharedSpaceSession.runtime.");
            if (messageChannel < 64 || messageChannel > 255) throw new InvalidOperationException("Spatial channel must be 64–255.");
            if (subscribed) return;
            runtime.UserMessageReceived += Receive;
            runtime.RemotePeerLeft += Left;
            runtime.ConnectionStateChanged += ConnectionChanged;
            subscribed = true;
        }
        public void BeginHost(SpaceMode mode, Pose anchorPlacement)
        {
            Subscribe(); ResetSession(); IsHost = true; authority = runtime.LocalPeer;
            Mode = mode; Epoch = Guid.NewGuid(); group = Guid.NewGuid();
            StartAlignment(anchorPlacement);
        }
        public void BeginClient(PeerId host)
        {
            if (!host.IsValid) throw new ArgumentException("Explicit host PeerId required.", nameof(host));
            Subscribe(); ResetSession(); authority = host;
            SetState(SpaceState.WaitingForHost, "Waiting for host's space mode");
        }
        public void Retry()
        {
            if (State == SpaceState.Offline || State == SpaceState.WaitingForHost) return;
            if (IsHost) BeginHost(Mode, Frame);
            else StartAlignment(Pose.identity);
        }
        public void End() { ResetSession(); SetState(SpaceState.Offline, "Not connected"); }
        void ResetSession()
        {
            operation?.Cancel(); operation?.Dispose(); operation = null;
            anchorProvider?.Release(); readyPeers.Clear(); Epoch = Guid.Empty;
            anchor = Guid.Empty; group = Guid.Empty; IsHost = false; heartbeat = 0;
        }
        async void StartAlignment(Pose placement)
        {
            operation?.Cancel(); operation?.Dispose();
            var current = operation = new CancellationTokenSource(TimeSpan.FromSeconds(Mathf.Max(1, alignmentTimeout)));
            readyPeers.Clear(); anchorProvider?.Release();
            SetState(SpaceState.Aligning, Mode == SpaceMode.Colocated ? "Aligning shared anchor…" : "Setting virtual space…");
            Publish();
            try
            {
                if (Mode == SpaceMode.Colocated)
                {
                    if (!anchorProvider) throw new InvalidOperationException("Shared anchor provider is not installed. Choose Separate spaces or install a provider.");
                    Task work;
                    if (IsHost)
                    {
                        var creation = anchorProvider.CreateAndShareAsync(group, placement, current.Token);
                        work = creation;
                        await WithCancellation(work, current.Token);
                        current.Token.ThrowIfCancellationRequested();
                        if (operation != current) return;
                        anchor = await creation;
                    }
                    else await WithCancellation(anchorProvider.LoadAsync(group, anchor, current.Token), current.Token);
                    if (!anchorProvider.IsLocalized) throw new InvalidOperationException("Shared anchor is not localized.");
                }
                current.Token.ThrowIfCancellationRequested();
                if (operation != current) return;
                SetState(SpaceState.Ready, Mode == SpaceMode.Colocated ? "Same room / anchor aligned" : "Separate spaces / virtual placement");
                Publish();
            }
            catch (Exception error)
            {
                if (operation != current) return;
                current.Cancel(); anchorProvider?.Release();
                SetState(SpaceState.Failed, error is OperationCanceledException
                    ? "Alignment timed out. Check internet, Enhanced Spatial Services and room visibility; retry or choose Separate spaces."
                    : error.Message);
                Publish();
            }
        }
        static async Task WithCancellation(Task work, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(work, cancelled.Task) != work)
                {
                    // Observe late native failures; provider also checks cancellation before binding.
                    _ = Observe(work);
                    token.ThrowIfCancellationRequested();
                }
                await work;
            }
        }
        static async Task Observe(Task work) { try { await work; } catch { } }
        void Update()
        {
            if (State == SpaceState.Offline || !runtime || runtime.State != ConnectionState.Joined) return;
            if (State == SpaceState.Ready && Mode == SpaceMode.Colocated && !IsReady)
            {
                readyPeers.Clear(); SetState(SpaceState.Failed, "Anchor tracking lost. Stop and retry alignment.");
            }
            if (Time.unscaledTime >= heartbeat) { heartbeat = Time.unscaledTime + .5f; Publish(); }
        }
        void Publish()
        {
            if (!runtime || runtime.State != ConnectionState.Joined || Epoch == Guid.Empty) return;
            string payload = IsHost
                ? $"tmspace1|host|{Epoch:N}|{(int)Mode}|{group:N}|{anchor:N}|{(IsReady ? 1 : 0)}"
                : $"tmspace1|peer|{Epoch:N}|{(IsReady ? 1 : 0)}";
            runtime.SendUserMessage((byte)messageChannel, new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)));
        }
        void Receive(NetworkMessage message)
        {
            if (State == SpaceState.Offline || message.Channel != messageChannel || message.Payload.Array == null || message.Payload.Count > 256) return;
            string[] f = Encoding.UTF8.GetString(message.Payload.Array, message.Payload.Offset, message.Payload.Count).Split('|');
            if (f.Length < 4 || f[0] != "tmspace1" || !Guid.TryParseExact(f[2], "N", out var epoch) || epoch == Guid.Empty) return;
            if (!IsHost && message.Sender == authority && f.Length == 7 && f[1] == "host" &&
                int.TryParse(f[3], out int mode) && mode >= 0 && mode <= 1 &&
                Guid.TryParseExact(f[4], "N", out var receivedGroup) && Guid.TryParseExact(f[5], "N", out var receivedAnchor) &&
                (f[6] == "0" || f[6] == "1"))
            {
                if (epoch != Epoch)
                {
                    operation?.Cancel(); operation?.Dispose(); operation = null;
                    anchorProvider?.Release(); readyPeers.Clear();
                    Epoch = epoch; Mode = (SpaceMode)mode; group = receivedGroup; anchor = receivedAnchor;
                    SetState(SpaceState.WaitingForHost, "Host is preparing the shared space…");
                }
                if (f[6] == "1")
                {
                    readyPeers[authority] = Time.unscaledTime;
                    if (State == SpaceState.WaitingForHost)
                    {
                        anchor = receivedAnchor;
                        if (Mode == SpaceMode.Colocated && (group == Guid.Empty || anchor == Guid.Empty)) return;
                        StartAlignment(Pose.identity);
                        readyPeers[authority] = Time.unscaledTime;
                    }
                }
                else readyPeers.Remove(authority);
            }
            else if (IsHost && f.Length == 4 && f[1] == "peer" && epoch == Epoch)
            {
                if (f[3] == "1") readyPeers[message.Sender] = Time.unscaledTime;
                else readyPeers.Remove(message.Sender);
            }
        }
        void Left(PeerId peer) { readyPeers.Remove(peer); if (!IsHost && peer == authority) End(); }
        void ConnectionChanged(ConnectionState state) { if (state == ConnectionState.Ready || state == ConnectionState.Offline || state == ConnectionState.Failed) End(); }
        void SetState(SpaceState value, string text) { State = value; Status = text; Changed?.Invoke(); }
        void OnDestroy()
        {
            End();
            if (runtime && subscribed)
            {
                runtime.UserMessageReceived -= Receive; runtime.RemotePeerLeft -= Left;
                runtime.ConnectionStateChanged -= ConnectionChanged;
            }
        }
    }
}
