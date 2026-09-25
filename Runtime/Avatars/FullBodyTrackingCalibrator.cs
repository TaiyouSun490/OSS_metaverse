using System;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public enum FullBodyTrackerRole
    {
        Head,
        Hips,
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot,
        Chest,
        LeftKnee,
        RightKnee,
        LeftElbow,
        RightElbow
    }

    [Serializable]
    public sealed class FullBodyTrackerBinding
    {
        public FullBodyTrackerRole role;
        public Transform source;
        public Transform target;
        public bool required = true;
        [HideInInspector] public bool calibrated;
        [HideInInspector] public Vector3 positionOffset;
        [HideInInspector] public Quaternion rotationOffset = Quaternion.identity;
    }

    [Serializable]
    internal sealed class FullBodyCalibrationSnapshot
    {
        public FullBodyCalibrationEntry[] entries = Array.Empty<FullBodyCalibrationEntry>();
    }

    [Serializable]
    internal sealed class FullBodyCalibrationEntry
    {
        public FullBodyTrackerRole role;
        public Vector3 positionOffset;
        public Quaternion rotationOffset = Quaternion.identity;
    }

    [DisallowMultipleComponent]
    public sealed class FullBodyTrackingCalibrator : MonoBehaviour
    {
        [SerializeField] private FullBodyTrackerBinding[] bindings = Array.Empty<FullBodyTrackerBinding>();
        [SerializeField] private string calibrationKey = "default";
        [SerializeField] private bool autoLoadCalibration = true;
        [SerializeField] private bool applyInLateUpdate = true;

        public event Action Calibrated;
        public bool IsCalibrated { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public FullBodyTrackerBinding[] Bindings => bindings;

        private string PlayerPrefsKey => "Taiyo.Metaverse.FBT." + (string.IsNullOrWhiteSpace(calibrationKey) ? "default" : calibrationKey);

        private void OnEnable()
        {
            if (autoLoadCalibration)
                LoadCalibration();
        }

        private void LateUpdate()
        {
            if (applyInLateUpdate)
                ApplyCalibratedPose();
        }

        public void SetBindings(params FullBodyTrackerBinding[] value)
        {
            bindings = value ?? Array.Empty<FullBodyTrackerBinding>();
            IsCalibrated = false;
        }

        public void SetSource(FullBodyTrackerRole role, Transform source)
        {
            var binding = FindOrCreate(role);
            if (binding != null)
                binding.source = source;
        }

        public void SetTarget(FullBodyTrackerRole role, Transform target)
        {
            var binding = FindOrCreate(role);
            if (binding != null)
                binding.target = target;
        }

        public bool Calibrate(bool save = true)
        {
            if (bindings == null || bindings.Length == 0)
                return Fail("No full-body tracker bindings are configured.");

            foreach (var binding in bindings)
            {
                if (binding == null)
                    continue;
                if (binding.source == null || binding.target == null)
                {
                    binding.calibrated = false;
                    if (binding.required)
                        return Fail($"Required {binding.role} source or target is missing.");
                    continue;
                }

                binding.positionOffset = binding.source.InverseTransformPoint(binding.target.position);
                binding.rotationOffset = Quaternion.Inverse(binding.source.rotation) * binding.target.rotation;
                binding.calibrated = true;
            }

            IsCalibrated = true;
            LastError = string.Empty;
            if (save)
                SaveCalibration();
            Calibrated?.Invoke();
            return true;
        }

        public void ApplyCalibratedPose()
        {
            if (!IsCalibrated || bindings == null)
                return;
            foreach (var binding in bindings)
            {
                if (binding == null || !binding.calibrated || binding.source == null || binding.target == null)
                    continue;
                binding.target.SetPositionAndRotation(
                    binding.source.TransformPoint(binding.positionOffset),
                    binding.source.rotation * binding.rotationOffset);
            }
        }

        public void ResetCalibration(bool deleteSaved = false)
        {
            IsCalibrated = false;
            LastError = string.Empty;
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    if (binding != null)
                        binding.calibrated = false;
                }
            }
            if (deleteSaved)
                PlayerPrefs.DeleteKey(PlayerPrefsKey);
        }

        public void SaveCalibration()
        {
            if (!IsCalibrated || bindings == null)
                return;
            var entries = new FullBodyCalibrationEntry[bindings.Length];
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                entries[index] = new FullBodyCalibrationEntry
                {
                    role = binding.role,
                    positionOffset = binding.positionOffset,
                    rotationOffset = binding.rotationOffset
                };
            }
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(new FullBodyCalibrationSnapshot { entries = entries }));
            PlayerPrefs.Save();
        }

        public bool LoadCalibration()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey) || bindings == null)
                return false;
            var snapshot = JsonUtility.FromJson<FullBodyCalibrationSnapshot>(PlayerPrefs.GetString(PlayerPrefsKey));
            if (snapshot?.entries == null)
                return false;

            var loaded = 0;
            foreach (var entry in snapshot.entries)
            {
                var binding = Find(entry.role);
                if (binding == null)
                    continue;
                binding.positionOffset = entry.positionOffset;
                binding.rotationOffset = entry.rotationOffset;
                binding.calibrated = true;
                loaded++;
            }
            IsCalibrated = loaded > 0;
            return IsCalibrated;
        }

        private FullBodyTrackerBinding Find(FullBodyTrackerRole role)
        {
            if (bindings == null)
                return null;
            foreach (var binding in bindings)
            {
                if (binding != null && binding.role == role)
                    return binding;
            }
            return null;
        }

        private FullBodyTrackerBinding FindOrCreate(FullBodyTrackerRole role)
        {
            var binding = Find(role);
            if (binding != null)
                return binding;
            var length = bindings != null ? bindings.Length : 0;
            Array.Resize(ref bindings, length + 1);
            binding = new FullBodyTrackerBinding
            {
                role = role,
                required = role == FullBodyTrackerRole.Hips || role == FullBodyTrackerRole.LeftFoot || role == FullBodyTrackerRole.RightFoot
            };
            bindings[length] = binding;
            return binding;
        }

        private bool Fail(string message)
        {
            IsCalibrated = false;
            LastError = message;
            return false;
        }
    }
}
