using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Taiyo.Metaverse.Tests
{
    public sealed class TestAnchorProvider : SharedAnchorProvider
    {
        public bool tracked = true, fail, pending;
        public int loads;
        public Pose pose = Pose.identity;
        [NonSerialized] public TaskCompletionSource<Guid> completion;
        public override bool IsLocalized => tracked;
        public override Pose AnchorPose => pose;
        public override Task<Guid> CreateAndShareAsync(Guid group, Pose value, CancellationToken token)
        {
            if (fail) throw new InvalidOperationException("Services disabled");
            if (pending) return (completion = new TaskCompletionSource<Guid>()).Task;
            return Task.FromResult(Guid.NewGuid());
        }
        public override Task LoadAsync(Guid group, Guid anchor, CancellationToken token)
        { loads++; if (fail) throw new InvalidOperationException("Services disabled"); return Task.CompletedTask; }
        public override void Release() { }
    }
    public sealed class SharedSpaceTests
    {
        readonly List<Object> objects = new List<Object>();
        readonly List<MetaverseRuntime> runtimes = new List<MetaverseRuntime>();
        static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
        static void Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, null);
        async Task<SharedSpaceSession> Client(string room)
        {
            var go = new GameObject("Spatial test"); objects.Add(go);
            var config = ScriptableObject.CreateInstance<MetaverseConfig>(); objects.Add(config);
            var provider = ScriptableObject.CreateInstance<LoopbackNetworkProvider>(); objects.Add(provider);
            Set(config, "networkProvider", provider);
            var runtime = go.AddComponent<MetaverseRuntime>(); runtime.enabled = false;
            Set(runtime, "configuration", config); Set(runtime, "joinDefaultRoomOnStart", false);
            // In EditMode Unity does not invoke Awake automatically.
            if (typeof(MetaverseRuntime).GetField("lifetime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime) == null) Call(runtime, "Awake");
            runtimes.Add(runtime);
            await runtime.JoinAsync(new RoomRequest { roomId = room, capacity = 4 });
            var space = go.AddComponent<SharedSpaceSession>(); space.runtime = runtime;
            return space;
        }
        void Pump(params SharedSpaceSession[] clients)
        {
            for (int i = 0; i < 3; i++)
                foreach (var c in clients) { Call(c, "Publish"); Call(c.runtime, "Update"); }
        }
        [TearDown] public async Task Cleanup()
        {
            foreach (var runtime in runtimes) if (runtime) await runtime.LeaveAsync();
            foreach (var runtime in runtimes) if (runtime) Call(runtime, "OnDestroy");
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear(); runtimes.Clear();
        }
        [TestCase(0)] [TestCase(90)] [TestCase(-135)]
        public void Coordinates_PreserveDistanceShapeAndRoundTrip(float yaw)
        {
            var frame = new Pose(new Vector3(4, -2, 9), Quaternion.Euler(15, yaw, -8));
            var hand = new Pose(new Vector3(.3f, 1.2f, -.8f), Quaternion.Euler(24, -18, 72));
            var restored = SpaceCoordinates.ToWorld(frame, SpaceCoordinates.ToLocal(frame, hand));
            Assert.That(Vector3.Distance(restored.position, hand.position), Is.LessThan(.00001));
            Assert.That(Quaternion.Angle(restored.rotation, hand.rotation), Is.LessThan(.01));
            var second = new Pose(hand.position + new Vector3(.1f, .2f, .3f), hand.rotation);
            Assert.That(Vector3.Distance(SpaceCoordinates.ToLocal(frame, hand).position, SpaceCoordinates.ToLocal(frame, second).position),
                Is.EqualTo(Vector3.Distance(hand.position, second.position)).Within(.00001));
        }
        [Test] public void Packet_RejectsOldEpochTruncationAndHonorsSlice()
        {
            var epoch = Guid.NewGuid(); var data = SpacePacket.Wrap(epoch, new byte[] { 4, 5, 6 });
            var padded = new byte[data.Length + 8]; Array.Copy(data, 0, padded, 3, data.Length);
            Assert.That(SpacePacket.TryUnwrap(new ArraySegment<byte>(padded, 3, data.Length), epoch, out var payload), Is.True);
            Assert.That(payload.Count, Is.EqualTo(3)); Assert.That(payload.Array[payload.Offset], Is.EqualTo(4));
            Assert.That(SpacePacket.TryUnwrap(new ArraySegment<byte>(data), Guid.NewGuid(), out _), Is.False);
            Assert.That(SpacePacket.TryUnwrap(new ArraySegment<byte>(data, 0, 15), epoch, out _), Is.False);
        }
        [Test] public async Task SeparateSpaces_HandshakeAndHostAuthorityAndDisconnect()
        {
            string room = Guid.NewGuid().ToString(); var host = await Client(room); var client = await Client(room); var other = await Client(room);
            client.BeginClient(host.runtime.LocalPeer); other.BeginClient(host.runtime.LocalPeer);
            host.BeginHost(SpaceMode.SeparateSpaces, Pose.identity); Pump(host, client, other);
            Assert.That(host.IsPeerReady(client.runtime.LocalPeer), Is.True); Assert.That(client.IsPeerReady(host.runtime.LocalPeer), Is.True);
            Assert.That(client.IsPeerReady(other.runtime.LocalPeer), Is.True); Assert.That(other.IsPeerReady(client.runtime.LocalPeer), Is.True);
            var epoch = client.Epoch;
            other.runtime.SendUserMessage(80, new ArraySegment<byte>(Encoding.UTF8.GetBytes($"tmspace1|host|{Guid.NewGuid():N}|1|{Guid.NewGuid():N}|{Guid.NewGuid():N}|1")));
            Pump(host, client, other); Assert.That(client.Epoch, Is.EqualTo(epoch));
            await host.runtime.LeaveAsync(); Pump(client, other); Assert.That(client.State, Is.EqualTo(SpaceState.Offline));
        }
        [Test] public async Task Colocated_FailureRetryLossAndSwitchToSeparate()
        {
            string room = Guid.NewGuid().ToString(); var host = await Client(room); var client = await Client(room);
            var hostAnchor = host.gameObject.AddComponent<TestAnchorProvider>(); host.anchorProvider = hostAnchor;
            var clientAnchor = client.gameObject.AddComponent<TestAnchorProvider>(); client.anchorProvider = clientAnchor; clientAnchor.fail = true;
            client.BeginClient(host.runtime.LocalPeer); host.BeginHost(SpaceMode.Colocated, Pose.identity); Pump(host, client);
            Assert.That(client.State, Is.EqualTo(SpaceState.Failed)); Assert.That(host.IsPeerReady(client.runtime.LocalPeer), Is.False);
            clientAnchor.fail = false; client.Retry(); Pump(host, client);
            Assert.That(client.Mode, Is.EqualTo(SpaceMode.Colocated)); Assert.That(host.IsPeerReady(client.runtime.LocalPeer), Is.True);
            clientAnchor.tracked = false; Call(client, "Update"); Pump(host, client);
            Assert.That(client.State, Is.EqualTo(SpaceState.Failed)); Assert.That(host.IsPeerReady(client.runtime.LocalPeer), Is.False);
            var oldEpoch = client.Epoch;
            host.BeginHost(SpaceMode.SeparateSpaces, Pose.identity); Pump(host, client);
            Assert.That(client.Mode, Is.EqualTo(SpaceMode.SeparateSpaces)); Assert.That(client.IsReady, Is.True);
            Assert.That(client.Epoch, Is.Not.EqualTo(oldEpoch));
        }
        [Test] public async Task CancelledNativeCompletionCannotReplaceNewMode()
        {
            var host = await Client(Guid.NewGuid().ToString());
            var provider = host.gameObject.AddComponent<TestAnchorProvider>(); provider.pending = true; host.anchorProvider = provider;
            host.BeginHost(SpaceMode.Colocated, Pose.identity); var pending = provider.completion;
            host.BeginHost(SpaceMode.SeparateSpaces, Pose.identity); var epoch = host.Epoch;
            pending.SetResult(Guid.NewGuid()); await Task.Yield();
            Assert.That(host.Mode, Is.EqualTo(SpaceMode.SeparateSpaces)); Assert.That(host.Epoch, Is.EqualTo(epoch)); Assert.That(host.IsReady, Is.True);
        }
    }
}
