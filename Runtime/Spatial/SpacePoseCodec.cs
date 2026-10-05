using System;

namespace Taiyo.Metaverse
{
    /// <summary>Epoch prevents queued poses from an earlier alignment entering a new coordinate frame.</summary>
    public static class SpacePoseCodec
    {
        public const byte Channel = 4;
        public static byte[] Encode(Guid epoch, TrackedPose pose)
        {
            return SpacePacket.Wrap(epoch, PoseCodec.Encode(pose));
        }
        public static bool TryDecode(ArraySegment<byte> data, Guid epoch, out TrackedPose pose)
        {
            pose = default;
            return SpacePacket.TryUnwrap(data, epoch, out var payload) && PoseCodec.TryDecode(payload, out pose);
        }
    }
}
