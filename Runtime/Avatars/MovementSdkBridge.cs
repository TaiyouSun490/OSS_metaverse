using System;
using System.Collections.Generic;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [DisallowMultipleComponent]
    public sealed class MovementSdkBridge : MonoBehaviour
    {
        private static readonly string[] KnownTypeMarkers =
        {
            "CharacterRetargeter",
            "RetargetingLayer",
            "NetworkCharacterRetargeter",
            "OVRBody",
            "OVRSkeleton"
        };

        [SerializeField] private MonoBehaviour[] trackingBehaviours = Array.Empty<MonoBehaviour>();
        [SerializeField] private bool autoDiscoverOnEnable = true;

        public IReadOnlyList<MonoBehaviour> TrackingBehaviours => trackingBehaviours;
        public bool IsSdkLoaded => IsAssemblyLoaded("Meta.XR.Movement") || IsAssemblyLoaded("Oculus.Movement");

        private void OnEnable()
        {
            if (autoDiscoverOnEnable && (trackingBehaviours == null || trackingBehaviours.Length == 0))
                DiscoverTrackingBehaviours();
        }

        public void SetTrackingBehaviours(params MonoBehaviour[] behaviours)
        {
            trackingBehaviours = behaviours ?? Array.Empty<MonoBehaviour>();
        }

        public int DiscoverTrackingBehaviours()
        {
            var discovered = new List<MonoBehaviour>();
            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == this || behaviour is AvatarMotionPipeline ||
                    behaviour is FinalIkVrikBridge || behaviour is HumanoidPoseSource)
                    continue;
                var name = behaviour.GetType().FullName ?? behaviour.GetType().Name;
                if (ContainsMarker(name))
                    discovered.Add(behaviour);
            }
            trackingBehaviours = discovered.ToArray();
            return trackingBehaviours.Length;
        }

        public void SetTrackingEnabled(bool value)
        {
            if (trackingBehaviours == null)
                return;
            foreach (var behaviour in trackingBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = value;
            }
        }

        private static bool ContainsMarker(string typeName)
        {
            foreach (var marker in KnownTypeMarkers)
            {
                if (typeName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool IsAssemblyLoaded(string prefix)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
