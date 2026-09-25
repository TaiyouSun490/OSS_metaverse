using UnityEditor;
using UnityEngine;

namespace Taiyo.Metaverse.Editor
{
    public static class AvatarMotionSetupMenu
    {
        [MenuItem("GameObject/Taiyo Metaverse/Configure Avatar Motion", false, 10)]
        private static void ConfigureSelectedAvatar()
        {
            var avatar = Selection.activeGameObject;
            if (avatar == null)
                return;

            var animator = avatar.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                EditorUtility.DisplayDialog(
                    "Taiyo Metaverse",
                    "Select an avatar with a configured Humanoid Animator.",
                    "OK");
                return;
            }

            var movement = GetOrAdd<MovementSdkBridge>(avatar);
            var finalIk = GetOrAdd<FinalIkVrikBridge>(avatar);
            var pipeline = GetOrAdd<AvatarMotionPipeline>(avatar);
            var poseSource = GetOrAdd<HumanoidPoseSource>(avatar);

            poseSource.Configure(animator, avatar.transform);
            movement.DiscoverTrackingBehaviours();
            pipeline.SetBridges(movement, finalIk);

            EditorUtility.SetDirty(movement);
            EditorUtility.SetDirty(finalIk);
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(poseSource);
            Selection.activeGameObject = avatar;

            Debug.Log(
                "Taiyo Metaverse avatar motion components were added. " +
                "Assign the HMD/controller targets on FinalIkVrikBridge, then assign HumanoidPoseSource " +
                "to MetaverseRuntime.localPoseSource.",
                avatar);
        }

        [MenuItem("GameObject/Taiyo Metaverse/Configure Avatar Motion", true)]
        private static bool ValidateConfigureSelectedAvatar() => Selection.activeGameObject != null;

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }
    }
}
