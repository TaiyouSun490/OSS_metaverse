using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class LoopbackNetworkProviderTests
    {
        [Test]
        public async Task TwoClients_CanJoinAndExchangeMessage()
        {
            var first = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            var second = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            try
            {
                await first.InitializeAsync(CancellationToken.None);
                await second.InitializeAsync(CancellationToken.None);
                var room = new RoomRequest { roomId = Guid.NewGuid().ToString("N"), capacity = 2 };
                await first.JoinAsync(room, CancellationToken.None);
                await second.JoinAsync(room, CancellationToken.None);
                second.Tick(0f);

                NetworkMessage received = default;
                var didReceive = false;
                second.MessageReceived += message => { received = message; didReceive = true; };
                first.Send(MetaverseChannels.UserStart, new ArraySegment<byte>(new byte[] { 1, 2, 3 }), DeliveryMode.Reliable);
                second.Tick(0f);

                Assert.That(didReceive, Is.True);
                Assert.That(received.Sender, Is.EqualTo(first.LocalPeer));
                Assert.That(received.Payload.Count, Is.EqualTo(3));
                await first.LeaveAsync(CancellationToken.None);
                await second.LeaveAsync(CancellationToken.None);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }
    }
}
