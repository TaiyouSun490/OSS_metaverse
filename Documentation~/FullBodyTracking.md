# Full-body tracking and calibration

Taiyo Metaverse uses one calibration and avatar-output path for both PC trackers and standalone motion-capture receivers:

```text
SteamVR trackers OR mocopi-driven Humanoid
  -> FullBodyTrackingCalibrator
  -> pelvis/left-foot/right-foot targets
  -> Final IK VRIK
  -> HumanoidPoseSource
  -> network pose (head, hands, hips, feet)
```

The calibration stores each tracker's position and rotation offset from its avatar target. It is saved in `PlayerPrefs` under a configurable profile key, so users normally calibrate once per tracker mounting/avatar combination. Recalibrate after moving a tracker, changing shoes, changing avatar proportions, or resetting the tracking origin.

## Common avatar setup

1. Configure Final IK VRIK on a valid Humanoid avatar.
2. Select the avatar and run **GameObject > Taiyo Metaverse > Configure Avatar Motion**.
3. Run **GameObject > Taiyo Metaverse > Configure Full Body Tracking**.
4. The second command creates pelvis and foot targets and connects `FullBodyTrackingCalibrator`, `FinalIkVrikBridge`, and `AvatarRig`.
5. Add `FullBodyCalibrationHud` or call `FullBodyTrackingCalibrator.Calibrate()` from your own UI.

Calibrate in a neutral upright pose, facing the application's forward direction, with feet parallel and approximately shoulder-width apart. The target avatar should also be in its neutral standing pose when calibration is captured.

## SteamVR trackers on PC

1. Install Valve's SteamVR Unity Plugin and complete SteamVR Input setup.
2. Create one tracked GameObject each for waist, left foot, and right foot. Optional trackers can be used for chest and knees.
3. Put `SteamVR_Behaviour_Pose` on each tracked object and bind each physical tracker to the corresponding pose action/input source.
4. Add `SteamVrFullBodySource` to the avatar and assign the three required tracker transforms plus its `FullBodyTrackingCalibrator`.
5. Enter Play mode, verify every tracker object follows the correct physical tracker, stand upright, then calibrate.

The Taiyo component intentionally does not compile against Valve types. It validates the component by runtime type name, which keeps SteamVR an optional dependency and avoids pulling Valve code into the UPM package.

## mocopi on standalone Quest

Sony's mocopi Receiver Plugin can build for Android and receives `mocopi (UDP)` motion from the mocopi app. Quest is an Android target, but Sony's public setup pages list Android rather than explicitly certifying each Quest model, so validate the exact plugin/Quest/OS combination you ship.

Recommended topology:

1. Pair the mocopi sensors with the mocopi phone app.
2. Put the phone and Quest on the same LAN.
3. In the phone app, select `mocopi (UDP)` and set the Quest IPv4 address and receiver port (the Sony sample defaults to 12351).
4. Import Sony's mocopi Receiver Plugin and build it into the Quest application using IL2CPP/ARM64.
5. Let the Sony receiver drive a hidden Humanoid source avatar.
6. Add `MocopiHumanoidFullBodySource` to the visible avatar and assign the hidden source Animator plus the visible avatar's calibrator.
7. Start UDP reception, confirm the hidden source moves, then calibrate the visible avatar.

Using a separate hidden source avatar avoids fighting between Sony's retargeter and Final IK on the same skeleton. The receiver plugin is not redistributed by this package; follow Sony's repository/license and Android network-permission instructions.

## Network compatibility

The pose packet now includes hips and both feet. `PoseCodec.TryDecode` still accepts the older 121-byte head/hand packet, while new peers send the 205-byte FBT packet. Older application builds cannot decode the new packet, so update all participants before enabling mixed-version rooms.

Only compact targets are sent over the network. Remote clients reconstruct the body with VRIK; raw tracker identifiers and calibration offsets are never sent.
