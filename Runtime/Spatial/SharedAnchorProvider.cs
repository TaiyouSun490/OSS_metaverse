using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public enum SpaceMode { SeparateSpaces, Colocated }
    public enum SpaceState { Offline, WaitingForHost, Aligning, Ready, Failed }

    /// <summary>Optional platform integration. All poses are metres in Unity world space.</summary>
    public abstract class SharedAnchorProvider : MonoBehaviour
    {
        public abstract bool IsLocalized { get; }
        public abstract Pose AnchorPose { get; }
        public abstract Task<Guid> CreateAndShareAsync(Guid group, Pose pose, CancellationToken cancellation);
        public abstract Task LoadAsync(Guid group, Guid anchor, CancellationToken cancellation);
        public abstract void Release();
    }

    /// <summary>Rigid transforms deliberately ignore hierarchy scale: a metre remains a metre.</summary>
    public static class SpaceCoordinates
    {
        public static Pose ToLocal(Pose frame, Pose world) => new Pose(
            Quaternion.Inverse(frame.rotation) * (world.position - frame.position),
            Quaternion.Inverse(frame.rotation) * world.rotation);
        public static Pose ToWorld(Pose frame, Pose local) => new Pose(
            frame.position + frame.rotation * local.position, frame.rotation * local.rotation);
        public static TrackedPose Transform(TrackedPose value, Pose frame, bool toLocal)
        {
            void Map(ref Vector3 p, ref Quaternion q)
            {
                var result = toLocal ? ToLocal(frame, new Pose(p, q)) : ToWorld(frame, new Pose(p, q));
                p = result.position; q = result.rotation;
            }
            Map(ref value.rootPosition, ref value.rootRotation);
            if ((value.trackingFlags & 1) != 0) Map(ref value.headPosition, ref value.headRotation);
            if ((value.trackingFlags & 2) != 0) Map(ref value.leftHandPosition, ref value.leftHandRotation);
            if ((value.trackingFlags & 4) != 0) Map(ref value.rightHandPosition, ref value.rightHandRotation);
            if ((value.trackingFlags & 8) != 0) Map(ref value.hipsPosition, ref value.hipsRotation);
            if ((value.trackingFlags & 16) != 0) Map(ref value.leftFootPosition, ref value.leftFootRotation);
            if ((value.trackingFlags & 32) != 0) Map(ref value.rightFootPosition, ref value.rightFootRotation);
            return value;
        }
    }
}
