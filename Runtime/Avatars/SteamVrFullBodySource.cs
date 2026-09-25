using System;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class SteamVrFullBodySource : MonoBehaviour
    {
        [SerializeField] private FullBodyTrackingCalibrator calibrator;
        [SerializeField] private Transform waistTracker;
        [SerializeField] private Transform leftFootTracker;
        [SerializeField] private Transform rightFootTracker;
        [SerializeField] private Transform chestTracker;
        [SerializeField] private Transform leftKneeTracker;
        [SerializeField] private Transform rightKneeTracker;
        [SerializeField] private bool validateSteamVrPoseComponents = true;

        public bool IsConfigured => waistTracker != null && leftFootTracker != null && rightFootTracker != null;
        public string LastError { get; private set; } = string.Empty;

        private void Awake()
        {
            if (calibrator == null)
                calibrator = GetComponent<FullBodyTrackingCalibrator>();
            ApplySources();
        }

        public void Configure(
            FullBodyTrackingCalibrator targetCalibrator,
            Transform waist,
            Transform leftFoot,
            Transform rightFoot)
        {
            calibrator = targetCalibrator;
            waistTracker = waist;
            leftFootTracker = leftFoot;
            rightFootTracker = rightFoot;
            ApplySources();
        }

        public bool ApplySources()
        {
            if (calibrator == null)
                return Fail("A FullBodyTrackingCalibrator is required.");
            if (!IsConfigured)
                return Fail("SteamVR FBT requires waist, left-foot, and right-foot tracker transforms.");
            if (validateSteamVrPoseComponents &&
                (!HasSteamVrPose(waistTracker) || !HasSteamVrPose(leftFootTracker) || !HasSteamVrPose(rightFootTracker)))
                return Fail("Each tracker Transform must have a SteamVR_Behaviour_Pose component in its hierarchy.");

            calibrator.SetSource(FullBodyTrackerRole.Hips, waistTracker);
            calibrator.SetSource(FullBodyTrackerRole.LeftFoot, leftFootTracker);
            calibrator.SetSource(FullBodyTrackerRole.RightFoot, rightFootTracker);
            calibrator.SetSource(FullBodyTrackerRole.Chest, chestTracker);
            calibrator.SetSource(FullBodyTrackerRole.LeftKnee, leftKneeTracker);
            calibrator.SetSource(FullBodyTrackerRole.RightKnee, rightKneeTracker);
            LastError = string.Empty;
            return true;
        }

        public bool Calibrate()
        {
            return ApplySources() && calibrator.Calibrate();
        }

        private static bool HasSteamVrPose(Transform value)
        {
            if (value == null)
                return false;
            foreach (var behaviour in value.GetComponentsInParent<MonoBehaviour>(true))
            {
                var name = behaviour != null ? behaviour.GetType().FullName : null;
                if (string.Equals(name, "Valve.VR.SteamVR_Behaviour_Pose", StringComparison.Ordinal) ||
                    (name != null && name.EndsWith(".SteamVR_Behaviour_Pose", StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }

        private bool Fail(string message)
        {
            LastError = message;
            return false;
        }
    }
}
