using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class HumanoidPoseSource : TrackedPoseSource
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform rootOverride;

        public void Configure(Animator sourceAnimator, Transform root = null)
        {
            animator = sourceAnimator;
            rootOverride = root;
        }

        public override bool TryGetPose(out TrackedPose pose)
        {
            pose = default;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return false;

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var root = rootOverride != null ? rootOverride : hips != null ? hips : animator.transform;
            pose.rootPosition = root.position;
            pose.rootRotation = root.rotation;
            pose.timestamp = Time.realtimeSinceStartupAsDouble;

            byte flags = 0;
            ReadBone(HumanBodyBones.Head, 1, ref flags, ref pose.headPosition, ref pose.headRotation);
            ReadBone(HumanBodyBones.LeftHand, 2, ref flags, ref pose.leftHandPosition, ref pose.leftHandRotation);
            ReadBone(HumanBodyBones.RightHand, 4, ref flags, ref pose.rightHandPosition, ref pose.rightHandRotation);
            ReadBone(HumanBodyBones.Hips, 8, ref flags, ref pose.hipsPosition, ref pose.hipsRotation);
            ReadBone(HumanBodyBones.LeftFoot, 16, ref flags, ref pose.leftFootPosition, ref pose.leftFootRotation);
            ReadBone(HumanBodyBones.RightFoot, 32, ref flags, ref pose.rightFootPosition, ref pose.rightFootRotation);
            pose.trackingFlags = flags;
            return true;
        }

        private void ReadBone(
            HumanBodyBones bone,
            byte flag,
            ref byte flags,
            ref Vector3 position,
            ref Quaternion rotation)
        {
            var target = animator.GetBoneTransform(bone);
            if (target == null)
                return;
            position = target.position;
            rotation = target.rotation;
            flags |= flag;
        }
    }
}
