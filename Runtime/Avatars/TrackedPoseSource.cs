using UnityEngine;

namespace Taiyo.Metaverse
{
    public abstract class TrackedPoseSource : MonoBehaviour
    {
        public abstract bool TryGetPose(out TrackedPose pose);
    }

    public sealed class TransformPoseSource : TrackedPoseSource
    {
        [SerializeField] private Transform root;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        public override bool TryGetPose(out TrackedPose pose)
        {
            var rootTransform = root != null ? root : transform;
            pose = new TrackedPose
            {
                rootPosition = rootTransform.position,
                rootRotation = rootTransform.rotation,
                timestamp = Time.realtimeSinceStartupAsDouble
            };

            byte flags = 0;
            ReadOptional(head, ref pose.headPosition, ref pose.headRotation, 1, ref flags);
            ReadOptional(leftHand, ref pose.leftHandPosition, ref pose.leftHandRotation, 2, ref flags);
            ReadOptional(rightHand, ref pose.rightHandPosition, ref pose.rightHandRotation, 4, ref flags);
            pose.trackingFlags = flags;
            return true;
        }

        private static void ReadOptional(
            Transform source,
            ref Vector3 position,
            ref Quaternion rotation,
            byte flag,
            ref byte flags)
        {
            if (source == null)
                return;
            position = source.position;
            rotation = source.rotation;
            flags |= flag;
        }
    }
}
