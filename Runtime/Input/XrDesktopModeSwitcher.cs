using UnityEngine;
using UnityEngine.XR;

namespace Taiyo.Metaverse
{
    public sealed class XrDesktopModeSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject desktopRig;
        [SerializeField] private GameObject xrRig;
        [SerializeField] private bool preferXrWhenDeviceIsPresent = true;

        private void Start()
        {
            var useXr = preferXrWhenDeviceIsPresent && (XRSettings.enabled || XRSettings.isDeviceActive);
            if (desktopRig != null) desktopRig.SetActive(!useXr);
            if (xrRig != null) xrRig.SetActive(useXr);
        }
    }
}
