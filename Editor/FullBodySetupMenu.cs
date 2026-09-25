using UnityEditor;
using UnityEngine;

namespace Taiyo.Metaverse.Editor
{
    public static class FullBodySetupMenu
    {
        [MenuItem("GameObject/Taiyo Metaverse/Configure Full Body Tracking", false, 11)]
        private static void ConfigureFullBodyTracking()
        {
            var avatar = Selection.activeGameObject;
            var animator = avatar != null ? avatar.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                EditorUtility.DisplayDialog("Taiyo Metaverse", "Select a valid Humanoid avatar.", "OK");
                return;
            }

            var root = FindOrCreateChild(avatar.transform, "Taiyo FBT Targets", avatar.transform.position, avatar.transform.rotation);
            var hips = CreateTarget(root, "Hips Target", animator.GetBoneTransform(HumanBodyBones.Hips));
            var leftFoot = CreateTarget(root, "Left Foot Target", animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            var rightFoot = CreateTarget(root, "Right Foot Target", animator.GetBoneTransform(HumanBodyBones.RightFoot));

            var calibrator = GetOrAdd<FullBodyTrackingCalibrator>(avatar);
            calibrator.SetTarget(FullBodyTrackerRole.Hips, hips);
            calibrator.SetTarget(FullBodyTrackerRole.LeftFoot, leftFoot);
            calibrator.SetTarget(FullBodyTrackerRole.RightFoot, rightFoot);

            var finalIk = GetOrAdd<FinalIkVrikBridge>(avatar);
            finalIk.SetLowerBodyTargets(hips, leftFoot, rightFoot);
            var rig = GetOrAdd<AvatarRig>(avatar);
            rig.SetFullBodyTargets(hips, leftFoot, rightFoot);

            EditorUtility.SetDirty(calibrator);
            EditorUtility.SetDirty(finalIk);
            EditorUtility.SetDirty(rig);
            Selection.activeGameObject = avatar;
            Debug.Log(
                "FBT targets are ready. Add SteamVrFullBodySource or MocopiHumanoidFullBodySource, assign its inputs, then calibrate while standing upright.",
                avatar);
        }

        [MenuItem("GameObject/Taiyo Metaverse/Configure Full Body Tracking", true)]
        private static bool ValidateConfigureFullBodyTracking() => Selection.activeGameObject != null;

        private static Transform CreateTarget(Transform parent, string name, Transform bone)
        {
            var position = bone != null ? bone.position : parent.position;
            var rotation = bone != null ? bone.rotation : parent.rotation;
            return FindOrCreateChild(parent, name, position, rotation);
        }

        private static Transform FindOrCreateChild(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing;
            var value = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(value.gameObject, "Configure full body tracking");
            value.SetParent(parent, true);
            value.SetPositionAndRotation(position, rotation);
            return value;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }
    }
}
