using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class AvatarRig : MonoBehaviour
    {
        [SerializeField] private Transform rootTarget;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform hipsTarget;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;

        private TrackedPose targetPose;
        private bool hasPose;
        private float smoothing = 0.15f;

        public void SetSmoothing(float value) => smoothing = Mathf.Clamp01(value);

        public void SetFullBodyTargets(Transform hips, Transform leftFoot, Transform rightFoot)
        {
            hipsTarget = hips;
            leftFootTarget = leftFoot;
            rightFootTarget = rightFoot;
        }

        public void SetTargetPose(TrackedPose pose)
        {
            targetPose = pose;
            hasPose = true;
        }

        private void LateUpdate()
        {
            if (!hasPose)
                return;

            var blend = smoothing <= 0f
                ? 1f
                : 1f - Mathf.Pow(smoothing, Time.unscaledDeltaTime * 60f);

            Apply(rootTarget != null ? rootTarget : transform, targetPose.rootPosition, targetPose.rootRotation, blend);
            if ((targetPose.trackingFlags & 1) != 0) Apply(headTarget, targetPose.headPosition, targetPose.headRotation, blend);
            if ((targetPose.trackingFlags & 2) != 0) Apply(leftHandTarget, targetPose.leftHandPosition, targetPose.leftHandRotation, blend);
            if ((targetPose.trackingFlags & 4) != 0) Apply(rightHandTarget, targetPose.rightHandPosition, targetPose.rightHandRotation, blend);
            if ((targetPose.trackingFlags & 8) != 0) Apply(hipsTarget, targetPose.hipsPosition, targetPose.hipsRotation, blend);
            if ((targetPose.trackingFlags & 16) != 0) Apply(leftFootTarget, targetPose.leftFootPosition, targetPose.leftFootRotation, blend);
            if ((targetPose.trackingFlags & 32) != 0) Apply(rightFootTarget, targetPose.rightFootPosition, targetPose.rightFootRotation, blend);
        }

        private static void Apply(Transform target, Vector3 position, Quaternion rotation, float blend)
        {
            if (target == null)
                return;
            target.SetPositionAndRotation(
                Vector3.Lerp(target.position, position, blend),
                Quaternion.Slerp(target.rotation, rotation, blend));
        }
    }
}
