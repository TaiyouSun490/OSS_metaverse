using System;
using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class AvatarMotionBridgeTests
    {
        [Test]
        public void FinalIkBridge_AssignsVrikTargetsWithoutCompileTimeDependency()
        {
            var root = new GameObject("Final IK bridge test");
            var head = new GameObject("Head").transform;
            var pelvis = new GameObject("Pelvis").transform;
            var left = new GameObject("Left hand").transform;
            var right = new GameObject("Right hand").transform;
            var leftFoot = new GameObject("Left foot").transform;
            var rightFoot = new GameObject("Right foot").transform;
            try
            {
                var fakeVrik = root.AddComponent<FakeVrikComponent>();
                var bridge = root.AddComponent<FinalIkVrikBridge>();
                bridge.SetVrikComponent(fakeVrik);
                bridge.SetTargets(head, pelvis, left, right);
                bridge.SetFootTargets(leftFoot, rightFoot);

                Assert.That(bridge.Bind(), Is.True, bridge.LastError);
                Assert.That(fakeVrik.solver.spine.headTarget, Is.SameAs(head));
                Assert.That(fakeVrik.solver.spine.pelvisTarget, Is.SameAs(pelvis));
                Assert.That(fakeVrik.solver.leftArm.target, Is.SameAs(left));
                Assert.That(fakeVrik.solver.rightArm.target, Is.SameAs(right));
                Assert.That(fakeVrik.solver.leftLeg.target, Is.SameAs(leftFoot));
                Assert.That(fakeVrik.solver.rightLeg.target, Is.SameAs(rightFoot));
                Assert.That(fakeVrik.solver.spine.pelvisPositionWeight, Is.EqualTo(1f));
                Assert.That(fakeVrik.solver.spine.pelvisRotationWeight, Is.EqualTo(1f));
                Assert.That(fakeVrik.solver.leftLeg.positionWeight, Is.EqualTo(1f));
                Assert.That(fakeVrik.solver.rightLeg.rotationWeight, Is.EqualTo(1f));

                bridge.SetSolvingEnabled(false);
                Assert.That(fakeVrik.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(head.gameObject);
                UnityEngine.Object.DestroyImmediate(pelvis.gameObject);
                UnityEngine.Object.DestroyImmediate(left.gameObject);
                UnityEngine.Object.DestroyImmediate(right.gameObject);
                UnityEngine.Object.DestroyImmediate(leftFoot.gameObject);
                UnityEngine.Object.DestroyImmediate(rightFoot.gameObject);
            }
        }

        [Test]
        public void MovementBridge_ControlsAssignedSdkBehaviours()
        {
            var root = new GameObject("Movement SDK bridge test");
            try
            {
                var fakeTracking = root.AddComponent<FakeMovementBehaviour>();
                var bridge = root.AddComponent<MovementSdkBridge>();
                bridge.SetTrackingBehaviours(fakeTracking);

                bridge.SetTrackingEnabled(false);
                Assert.That(fakeTracking.enabled, Is.False);
                bridge.SetTrackingEnabled(true);
                Assert.That(fakeTracking.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    internal sealed class FakeMovementBehaviour : MonoBehaviour { }

    internal sealed class FakeVrikComponent : MonoBehaviour
    {
        public FakeVrikSolver solver = new FakeVrikSolver();
    }

    [Serializable]
    internal sealed class FakeVrikSolver
    {
        public FakeVrikSpine spine = new FakeVrikSpine();
        public FakeVrikArm leftArm = new FakeVrikArm();
        public FakeVrikArm rightArm = new FakeVrikArm();
        public FakeVrikLeg leftLeg = new FakeVrikLeg();
        public FakeVrikLeg rightLeg = new FakeVrikLeg();
    }

    [Serializable]
    internal sealed class FakeVrikSpine
    {
        public Transform headTarget;
        public Transform pelvisTarget;
        public float pelvisPositionWeight;
        public float pelvisRotationWeight;
    }

    [Serializable]
    internal sealed class FakeVrikArm
    {
        public Transform target;
    }

    [Serializable]
    internal sealed class FakeVrikLeg
    {
        public Transform target;
        public float positionWeight;
        public float rotationWeight;
    }
}
