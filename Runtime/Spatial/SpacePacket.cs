using System;
namespace Taiyo.Metaverse
{
    /// <summary>Use for application-specific hands, tools and other spatial payloads.</summary>
    public static class SpacePacket
    {
        public static byte[] Wrap(Guid epoch, byte[] payload)
        {
            if (epoch == Guid.Empty) throw new ArgumentException("Spatial epoch must be nonempty.", nameof(epoch));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            byte[] result = new byte[16 + payload.Length];
            Buffer.BlockCopy(epoch.ToByteArray(), 0, result, 0, 16);
            Buffer.BlockCopy(payload, 0, result, 16, payload.Length);
            return result;
        }
        public static bool TryUnwrap(ArraySegment<byte> packet, Guid epoch, out ArraySegment<byte> payload)
        {
            payload = default;
            if (epoch == Guid.Empty || packet.Array == null || packet.Count < 16) return false;
            var id = epoch.ToByteArray();
            for (int i = 0; i < 16; i++) if (packet.Array[packet.Offset + i] != id[i]) return false;
            payload = new ArraySegment<byte>(packet.Array, packet.Offset + 16, packet.Count - 16);
            return true;
        }
    }
}
