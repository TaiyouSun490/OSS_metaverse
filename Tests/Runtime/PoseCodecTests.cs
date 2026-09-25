using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class PoseCodecTests
    {
        [Test]
        public void RoundTrip_PreservesTrackedPose()
        {
            var source = new TrackedPose
            {
                rootPosition = new Vector3(1f, 2f, 3f),
                rootRotation = Quaternion.Euler(10f, 20f, 30f),
                headPosition = new Vector3(4f, 5f, 6f),
                headRotation = Quaternion.Euler(40f, 50f, 60f),
                leftHandPosition = Vector3.left,
                leftHandRotation = Quaternion.Euler(1f, 2f, 3f),
                rightHandPosition = Vector3.right,
                rightHandRotation = Quaternion.Euler(4f, 5f, 6f),
                hipsPosition = new Vector3(0f, 1f, 0f),
                hipsRotation = Quaternion.Euler(0f, 12f, 0f),
                leftFootPosition = new Vector3(-0.2f, 0f, 0f),
                leftFootRotation = Quaternion.Euler(0f, -5f, 0f),
                rightFootPosition = new Vector3(0.2f, 0f, 0f),
                rightFootRotation = Quaternion.Euler(0f, 5f, 0f),
                trackingFlags = 63,
                timestamp = 42.5
            };

            var bytes = PoseCodec.Encode(source);
            Assert.That(bytes, Has.Length.EqualTo(PoseCodec.PayloadSize));
            Assert.That(PoseCodec.TryDecode(new System.ArraySegment<byte>(bytes), out var decoded), Is.True);
            Assert.That(decoded.rootPosition, Is.EqualTo(source.rootPosition));
            Assert.That(decoded.headPosition, Is.EqualTo(source.headPosition));
            Assert.That(decoded.hipsPosition, Is.EqualTo(source.hipsPosition));
            Assert.That(decoded.leftFootPosition, Is.EqualTo(source.leftFootPosition));
            Assert.That(decoded.rightFootPosition, Is.EqualTo(source.rightFootPosition));
            Assert.That(decoded.trackingFlags, Is.EqualTo(63));
            Assert.That(decoded.timestamp, Is.EqualTo(42.5));
        }

        [Test]
        public void Decode_RejectsMalformedPacket()
        {
            Assert.That(PoseCodec.TryDecode(new System.ArraySegment<byte>(new byte[4]), out _), Is.False);
        }
    }
}
