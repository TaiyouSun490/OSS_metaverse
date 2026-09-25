using System;
using System.Reflection;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class FinalIkVrikBridge : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour vrikComponent;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform pelvisTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;
        [SerializeField] private bool bindOnEnable = true;

        public bool IsBound { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public MonoBehaviour VrikComponent => vrikComponent;

        private void OnEnable()
        {
            if (bindOnEnable)
                Bind();
        }

        public void SetVrikComponent(MonoBehaviour component)
        {
            vrikComponent = component;
            IsBound = false;
        }

        public void SetTargets(Transform head, Transform pelvis, Transform leftHand, Transform rightHand)
        {
            headTarget = head;
            pelvisTarget = pelvis;
            leftHandTarget = leftHand;
            rightHandTarget = rightHand;
            IsBound = false;
        }

        public void SetFootTargets(Transform leftFoot, Transform rightFoot)
        {
            leftFootTarget = leftFoot;
            rightFootTarget = rightFoot;
            IsBound = false;
        }

        public void SetLowerBodyTargets(Transform pelvis, Transform leftFoot, Transform rightFoot)
        {
            pelvisTarget = pelvis;
            leftFootTarget = leftFoot;
            rightFootTarget = rightFoot;
            IsBound = false;
        }

        public bool Bind()
        {
            if (vrikComponent == null)
                vrikComponent = FindVrikComponent();
            if (vrikComponent == null)
                return Fail("Final IK VRIK component was not found. Add VRIK to the avatar or assign it explicitly.");

            var solver = ReadMember(vrikComponent, "solver");
            if (solver == null)
                return Fail("The assigned component does not expose a VRIK solver.");

            var assigned = 0;
            assigned += SetNestedTarget(solver, "spine", "headTarget", headTarget) ? 1 : 0;
            assigned += SetNestedTarget(solver, "spine", "pelvisTarget", pelvisTarget) ? 1 : 0;
            assigned += SetNestedTarget(solver, "leftArm", "target", leftHandTarget) ? 1 : 0;
            assigned += SetNestedTarget(solver, "rightArm", "target", rightHandTarget) ? 1 : 0;
            assigned += SetNestedTarget(solver, "leftLeg", "target", leftFootTarget) ? 1 : 0;
            assigned += SetNestedTarget(solver, "rightLeg", "target", rightFootTarget) ? 1 : 0;
            if (pelvisTarget != null)
            {
                SetNestedValue(solver, "spine", "pelvisPositionWeight", 1f);
                SetNestedValue(solver, "spine", "pelvisRotationWeight", 1f);
            }
            if (leftFootTarget != null)
            {
                SetNestedValue(solver, "leftLeg", "positionWeight", 1f);
                SetNestedValue(solver, "leftLeg", "rotationWeight", 1f);
            }
            if (rightFootTarget != null)
            {
                SetNestedValue(solver, "rightLeg", "positionWeight", 1f);
                SetNestedValue(solver, "rightLeg", "rotationWeight", 1f);
            }

            if (assigned == 0)
                return Fail("No VRIK targets were assigned. Configure at least a head or hand target.");

            IsBound = true;
            LastError = string.Empty;
            return true;
        }

        public void SetSolvingEnabled(bool value)
        {
            if (vrikComponent == null)
                vrikComponent = FindVrikComponent();
            if (vrikComponent != null)
                vrikComponent.enabled = value;
        }

        private MonoBehaviour FindVrikComponent()
        {
            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null || behaviour == this)
                    continue;
                var type = behaviour.GetType();
                if (type.FullName == "RootMotion.FinalIK.VRIK" || type.Name == "VRIK")
                    return behaviour;
            }
            return null;
        }

        private bool Fail(string message)
        {
            IsBound = false;
            LastError = message;
            return false;
        }

        private static bool SetNestedTarget(object root, string groupName, string targetName, Transform value)
        {
            if (value == null)
                return false;
            var group = ReadMember(root, groupName);
            return group != null && WriteMember(group, targetName, value);
        }

        private static bool SetNestedValue(object root, string groupName, string memberName, object value)
        {
            var group = ReadMember(root, groupName);
            return group != null && WriteMember(group, memberName, value);
        }

        private static object ReadMember(object instance, string name)
        {
            if (instance == null)
                return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(instance);
            var property = type.GetProperty(name, flags);
            return property != null && property.CanRead ? property.GetValue(instance, null) : null;
        }

        private static bool WriteMember(object instance, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();
            var field = type.GetField(name, flags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(instance, value);
                return true;
            }

            var property = type.GetProperty(name, flags);
            if (property == null || !property.CanWrite || !property.PropertyType.IsInstanceOfType(value))
                return false;
            property.SetValue(instance, value, null);
            return true;
        }
    }
}
