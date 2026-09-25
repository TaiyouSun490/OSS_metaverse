using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class LegacyPoseCodecTests
    {
        [Test]
        public void Decode_AcceptsLegacyHeadAndHandsPacket()
        {
            byte[] bytes;
            using (var stream = new MemoryStream(PoseCodec.LegacyPayloadSize))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)7);
                WritePose(writer, Vector3.zero, Quaternion.identity);
                WritePose(writer, Vector3.up, Quaternion.identity);
                WritePose(writer, Vector3.left, Quaternion.identity);
                WritePose(writer, Vector3.right, Quaternion.identity);
                writer.Write(123.5d);
                bytes = stream.ToArray();
            }

            Assert.That(bytes, Has.Length.EqualTo(PoseCodec.LegacyPayloadSize));
            Assert.That(PoseCodec.TryDecode(new ArraySegment<byte>(bytes), out var pose), Is.True);
            Assert.That(pose.trackingFlags, Is.EqualTo(7));
            Assert.That(pose.headPosition, Is.EqualTo(Vector3.up));
            Assert.That(pose.hipsPosition, Is.EqualTo(Vector3.zero));
            Assert.That(pose.timestamp, Is.EqualTo(123.5d));
        }

        private static void WritePose(BinaryWriter writer, Vector3 position, Quaternion rotation)
        {
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(position.z);
            writer.Write(rotation.x);
            writer.Write(rotation.y);
            writer.Write(rotation.z);
            writer.Write(rotation.w);
        }
    }
}
