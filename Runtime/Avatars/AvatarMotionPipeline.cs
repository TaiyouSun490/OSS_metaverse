using UnityEngine;

namespace Taiyo.Metaverse
{
    public enum AvatarMotionMode
    {
        MovementSdkOnly,
        FinalIkOnly,
        MovementSdkThenFinalIk
    }

    [DisallowMultipleComponent]
    public sealed class AvatarMotionPipeline : MonoBehaviour
    {
        [SerializeField] private AvatarMotionMode mode = AvatarMotionMode.MovementSdkThenFinalIk;
        [SerializeField] private bool locallyControlled = true;
        [SerializeField] private bool driveRemoteAvatarWithFinalIk = true;
        [SerializeField] private MovementSdkBridge movementSdk;
        [SerializeField] private FinalIkVrikBridge finalIk;

        public AvatarMotionMode Mode => mode;
        public bool IsLocallyControlled => locallyControlled;

        private void Awake()
        {
            ResolveBridges();
        }

        private void OnEnable()
        {
            ResolveBridges();
            ApplyState();
        }

        public void SetMode(AvatarMotionMode value)
        {
            mode = value;
            ApplyState();
        }

        public void SetLocallyControlled(bool value)
        {
            locallyControlled = value;
            ApplyState();
        }

        public void SetBridges(MovementSdkBridge movementBridge, FinalIkVrikBridge finalIkBridge)
        {
            movementSdk = movementBridge;
            finalIk = finalIkBridge;
            ApplyState();
        }

        public void ApplyState()
        {
            ResolveBridges();

            var useMovement = locallyControlled &&
                (mode == AvatarMotionMode.MovementSdkOnly || mode == AvatarMotionMode.MovementSdkThenFinalIk);
            var useFinalIk = locallyControlled
                ? mode == AvatarMotionMode.FinalIkOnly || mode == AvatarMotionMode.MovementSdkThenFinalIk
                : driveRemoteAvatarWithFinalIk;

            movementSdk?.SetTrackingEnabled(useMovement);
            if (finalIk != null)
            {
                if (useFinalIk && !finalIk.IsBound)
                    finalIk.Bind();
                finalIk.SetSolvingEnabled(useFinalIk);
            }
        }

        private void ResolveBridges()
        {
            if (movementSdk == null)
                movementSdk = GetComponent<MovementSdkBridge>();
            if (finalIk == null)
                finalIk = GetComponent<FinalIkVrikBridge>();
        }
    }
}
