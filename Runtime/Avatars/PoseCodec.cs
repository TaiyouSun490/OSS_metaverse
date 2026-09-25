using System;
using System.IO;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public static class PoseCodec
    {
        public const int LegacyPayloadSize = 121;
        public const int PayloadSize = 205;

        public static byte[] Encode(TrackedPose pose)
        {
            using (var stream = new MemoryStream(PayloadSize))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(pose.trackingFlags);
                Write(writer, pose.rootPosition); Write(writer, pose.rootRotation);
                Write(writer, pose.headPosition); Write(writer, pose.headRotation);
                Write(writer, pose.leftHandPosition); Write(writer, pose.leftHandRotation);
                Write(writer, pose.rightHandPosition); Write(writer, pose.rightHandRotation);
                Write(writer, pose.hipsPosition); Write(writer, pose.hipsRotation);
                Write(writer, pose.leftFootPosition); Write(writer, pose.leftFootRotation);
                Write(writer, pose.rightFootPosition); Write(writer, pose.rightFootRotation);
                writer.Write(pose.timestamp);
                return stream.ToArray();
            }
        }

        public static bool TryDecode(ArraySegment<byte> payload, out TrackedPose pose)
        {
            pose = default;
            if (payload.Array == null || (payload.Count != PayloadSize && payload.Count != LegacyPayloadSize))
                return false;

            try
            {
                using (var stream = new MemoryStream(payload.Array, payload.Offset, payload.Count, false))
                using (var reader = new BinaryReader(stream))
                {
                    pose.trackingFlags = reader.ReadByte();
                    pose.rootPosition = ReadVector3(reader); pose.rootRotation = ReadQuaternion(reader);
                    pose.headPosition = ReadVector3(reader); pose.headRotation = ReadQuaternion(reader);
                    pose.leftHandPosition = ReadVector3(reader); pose.leftHandRotation = ReadQuaternion(reader);
                    pose.rightHandPosition = ReadVector3(reader); pose.rightHandRotation = ReadQuaternion(reader);
                    if (payload.Count == PayloadSize)
                    {
                        pose.hipsPosition = ReadVector3(reader); pose.hipsRotation = ReadQuaternion(reader);
                        pose.leftFootPosition = ReadVector3(reader); pose.leftFootRotation = ReadQuaternion(reader);
                        pose.rightFootPosition = ReadVector3(reader); pose.rightFootRotation = ReadQuaternion(reader);
                    }
                    pose.timestamp = reader.ReadDouble();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private static void Write(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x); writer.Write(value.y); writer.Write(value.z);
        }

        private static void Write(BinaryWriter writer, Quaternion value)
        {
            writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w);
        }

        private static Vector3 ReadVector3(BinaryReader reader) =>
            new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        private static Quaternion ReadQuaternion(BinaryReader reader) =>
            new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }
}
