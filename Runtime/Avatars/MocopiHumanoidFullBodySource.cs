using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class MocopiHumanoidFullBodySource : MonoBehaviour
    {
        [SerializeField] private Animator mocopiDrivenAnimator;
        [SerializeField] private FullBodyTrackingCalibrator calibrator;
        [SerializeField] private bool includeHeadAndHands = true;

        public string LastError { get; private set; } = string.Empty;

        private void Awake()
        {
            if (calibrator == null)
                calibrator = GetComponent<FullBodyTrackingCalibrator>();
            ApplySources();
        }

        public void Configure(Animator sourceAnimator, FullBodyTrackingCalibrator targetCalibrator)
        {
            mocopiDrivenAnimator = sourceAnimator;
            calibrator = targetCalibrator;
            ApplySources();
        }

        public bool ApplySources()
        {
            if (calibrator == null)
                return Fail("A FullBodyTrackingCalibrator is required.");
            if (mocopiDrivenAnimator == null || mocopiDrivenAnimator.avatar == null || !mocopiDrivenAnimator.avatar.isHuman)
                return Fail("mocopi must drive a valid Humanoid Animator.");

            Bind(FullBodyTrackerRole.Hips, HumanBodyBones.Hips);
            Bind(FullBodyTrackerRole.LeftFoot, HumanBodyBones.LeftFoot);
            Bind(FullBodyTrackerRole.RightFoot, HumanBodyBones.RightFoot);
            Bind(FullBodyTrackerRole.Chest, HumanBodyBones.Chest);
            Bind(FullBodyTrackerRole.LeftKnee, HumanBodyBones.LeftLowerLeg);
            Bind(FullBodyTrackerRole.RightKnee, HumanBodyBones.RightLowerLeg);
            Bind(FullBodyTrackerRole.LeftElbow, HumanBodyBones.LeftLowerArm);
            Bind(FullBodyTrackerRole.RightElbow, HumanBodyBones.RightLowerArm);
            if (includeHeadAndHands)
            {
                Bind(FullBodyTrackerRole.Head, HumanBodyBones.Head);
                Bind(FullBodyTrackerRole.LeftHand, HumanBodyBones.LeftHand);
                Bind(FullBodyTrackerRole.RightHand, HumanBodyBones.RightHand);
            }
            LastError = string.Empty;
            return true;
        }

        public bool Calibrate()
        {
            return ApplySources() && calibrator.Calibrate();
        }

        private void Bind(FullBodyTrackerRole role, HumanBodyBones bone)
        {
            calibrator.SetSource(role, mocopiDrivenAnimator.GetBoneTransform(bone));
        }

        private bool Fail(string message)
        {
            LastError = message;
            return false;
        }
    }
}
