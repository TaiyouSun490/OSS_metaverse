using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class RoomVisibilityTests
    {
        [Test]
        public async Task Discovery_ReturnsPublicRoomAndHidesPrivateRoom()
        {
            var publicHost = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            var privateHost = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            var browser = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            var suffix = Guid.NewGuid().ToString("N");
            try
            {
                await publicHost.InitializeAsync(CancellationToken.None);
                await privateHost.InitializeAsync(CancellationToken.None);
                await browser.InitializeAsync(CancellationToken.None);
                await publicHost.JoinAsync(new RoomRequest
                {
                    roomId = "public-" + suffix,
                    visibility = RoomVisibility.Public,
                    capacity = 8
                }, CancellationToken.None);
                await privateHost.JoinAsync(new RoomRequest
                {
                    roomId = "private-" + suffix,
                    visibility = RoomVisibility.Private,
                    accessToken = "invite-token",
                    capacity = 4
                }, CancellationToken.None);

                var rooms = await browser.DiscoverPublicRoomsAsync(CancellationToken.None);
                Assert.That(rooms.Any(room => room.Id == "public-" + suffix), Is.True);
                Assert.That(rooms.Any(room => room.Id == "private-" + suffix), Is.False);
            }
            finally
            {
                await publicHost.LeaveAsync(CancellationToken.None);
                await privateHost.LeaveAsync(CancellationToken.None);
                UnityEngine.Object.DestroyImmediate(publicHost);
                UnityEngine.Object.DestroyImmediate(privateHost);
                UnityEngine.Object.DestroyImmediate(browser);
            }
        }

        [Test]
        public async Task PrivateRoom_RejectsWrongTokenAndAcceptsInviteToken()
        {
            var host = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            var guest = ScriptableObject.CreateInstance<LoopbackNetworkProvider>();
            var roomId = "private-" + Guid.NewGuid().ToString("N");
            try
            {
                await host.InitializeAsync(CancellationToken.None);
                await guest.InitializeAsync(CancellationToken.None);
                await host.JoinAsync(new RoomRequest
                {
                    roomId = roomId,
                    visibility = RoomVisibility.Private,
                    accessToken = "valid-token"
                }, CancellationToken.None);

                Assert.Throws<UnauthorizedAccessException>(() => guest.JoinAsync(new RoomRequest
                {
                    roomId = roomId,
                    visibility = RoomVisibility.Private,
                    createIfMissing = false,
                    accessToken = "wrong-token"
                }, CancellationToken.None));
                Assert.That(guest.State, Is.EqualTo(ConnectionState.Ready));

                await guest.JoinAsync(new RoomRequest
                {
                    roomId = roomId,
                    visibility = RoomVisibility.Private,
                    createIfMissing = false,
                    accessToken = "valid-token"
                }, CancellationToken.None);
                Assert.That(guest.State, Is.EqualTo(ConnectionState.Joined));
            }
            finally
            {
                await guest.LeaveAsync(CancellationToken.None);
                await host.LeaveAsync(CancellationToken.None);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(guest);
            }
        }
    }
}
