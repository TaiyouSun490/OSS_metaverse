using System;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public enum ConnectionState
    {
        Offline,
        Initializing,
        Ready,
        Joining,
        Joined,
        Leaving,
        Failed
    }

    public enum DeliveryMode
    {
        Unreliable,
        Reliable
    }

    public enum RoomVisibility
    {
        Public,
        Private
    }

    public static class MetaverseChannels
    {
        public const byte Presence = 1;
        public const byte Pose = 2;
        public const byte Avatar = 3;
        public const byte Voice = 10;
        public const byte UserStart = 64;
    }

    [Serializable]
    public readonly struct PeerId : IEquatable<PeerId>
    {
        public readonly string Value;

        public PeerId(string value) => Value = value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(PeerId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PeerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);
    }

    [Serializable]
    public sealed class LocalUserProfile
    {
        public string userId = "guest";
        public string displayName = "Guest";
        public string avatarAddress = string.Empty;
    }

    [Serializable]
    public sealed class RoomRequest
    {
        public string roomId = "lobby";
        public RoomVisibility visibility = RoomVisibility.Public;
        [Min(1)] public int capacity = 32;
        public bool createIfMissing = true;
        public bool host;
        [Tooltip("Short-lived backend-issued token for a private room. Never use a reusable account password.")]
        public string accessToken = string.Empty;
    }

    public readonly struct RoomInfo
    {
        public readonly string Id;
        public readonly RoomVisibility Visibility;
        public readonly int PlayerCount;
        public readonly int Capacity;

        public RoomInfo(string id, RoomVisibility visibility, int playerCount, int capacity)
        {
            Id = id ?? string.Empty;
            Visibility = visibility;
            PlayerCount = playerCount;
            Capacity = capacity;
        }
    }

    public readonly struct PeerInfo
    {
        public readonly PeerId Id;
        public readonly bool IsLocal;

        public PeerInfo(PeerId id, bool isLocal)
        {
            Id = id;
            IsLocal = isLocal;
        }
    }

    public readonly struct NetworkMessage
    {
        public readonly PeerId Sender;
        public readonly byte Channel;
        public readonly ArraySegment<byte> Payload;

        public NetworkMessage(PeerId sender, byte channel, ArraySegment<byte> payload)
        {
            Sender = sender;
            Channel = channel;
            Payload = payload;
        }
    }

    [Serializable]
    public struct TrackedPose
    {
        public Vector3 rootPosition;
        public Quaternion rootRotation;
        public Vector3 headPosition;
        public Quaternion headRotation;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation;
        public Vector3 hipsPosition;
        public Quaternion hipsRotation;
        public Vector3 leftFootPosition;
        public Quaternion leftFootRotation;
        public Vector3 rightFootPosition;
        public Quaternion rightFootRotation;
        public byte trackingFlags;
        public double timestamp;
    }
}
