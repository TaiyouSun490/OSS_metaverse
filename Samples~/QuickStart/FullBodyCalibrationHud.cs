using UnityEngine;

namespace Taiyo.Metaverse.Samples
{
    public sealed class FullBodyCalibrationHud : MonoBehaviour
    {
        [SerializeField] private FullBodyTrackingCalibrator calibrator;
        private string status = "Stand upright, face forward, and press Calibrate.";

        private void Awake()
        {
            if (calibrator == null)
                calibrator = FindObjectOfType<FullBodyTrackingCalibrator>();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 400, 360, 150), GUI.skin.box);
            GUILayout.Label("Full Body Tracking");
            GUILayout.Label(calibrator != null && calibrator.IsCalibrated ? "State: Calibrated" : "State: Not calibrated");
            GUILayout.Label(status);
            if (calibrator != null && GUILayout.Button("Calibrate current upright pose"))
            {
                var success = calibrator.Calibrate();
                status = success ? "Calibration saved for this user/device profile." : calibrator.LastError;
            }
            if (calibrator != null && GUILayout.Button("Reset saved calibration"))
            {
                calibrator.ResetCalibration(true);
                status = "Calibration reset.";
            }
            GUILayout.EndArea();
        }
    }
}
