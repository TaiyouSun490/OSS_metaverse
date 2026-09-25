using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class FullBodyTrackingTests
    {
        [Test]
        public void Calibrate_PreservesMountOffsetAndFollowsTracker()
        {
            var root = new GameObject("FBT calibration test");
            var tracker = new GameObject("Waist tracker").transform;
            var target = new GameObject("Pelvis target").transform;
            try
            {
                tracker.SetPositionAndRotation(new Vector3(1f, 1f, 1f), Quaternion.Euler(0f, 30f, 0f));
                target.SetPositionAndRotation(new Vector3(1f, 0.9f, 1.1f), Quaternion.Euler(0f, 10f, 0f));

                var calibrator = root.AddComponent<FullBodyTrackingCalibrator>();
                calibrator.SetBindings(new FullBodyTrackerBinding
                {
                    role = FullBodyTrackerRole.Hips,
                    source = tracker,
                    target = target
                });
                Assert.That(calibrator.Calibrate(false), Is.True, calibrator.LastError);

                var localOffset = tracker.InverseTransformPoint(target.position);
                var rotationOffset = Quaternion.Inverse(tracker.rotation) * target.rotation;
                tracker.SetPositionAndRotation(new Vector3(2f, 1.2f, -1f), Quaternion.Euler(5f, 80f, 2f));
                calibrator.ApplyCalibratedPose();

                Assert.That(Vector3.Distance(target.position, tracker.TransformPoint(localOffset)), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(target.rotation, tracker.rotation * rotationOffset), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(tracker.gameObject);
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void Calibrate_FailsWhenRequiredTrackerIsMissing()
        {
            var root = new GameObject("FBT missing tracker test");
            var target = new GameObject("Foot target").transform;
            try
            {
                var calibrator = root.AddComponent<FullBodyTrackingCalibrator>();
                calibrator.SetBindings(new FullBodyTrackerBinding
                {
                    role = FullBodyTrackerRole.LeftFoot,
                    target = target,
                    required = true
                });
                Assert.That(calibrator.Calibrate(false), Is.False);
                Assert.That(calibrator.LastError, Does.Contain("LeftFoot"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }
        }
    }
}
