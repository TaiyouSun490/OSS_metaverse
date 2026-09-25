# Avatar motion: Meta Movement SDK and Final IK

The runtime uses an optional, staged avatar pipeline:

1. Meta XR Movement SDK reads body tracking and retargets the Humanoid avatar.
2. Final IK VRIK refines the head, pelvis, and hand constraints.
3. `HumanoidPoseSource` reads the resulting Humanoid pose for network transmission.
4. Remote avatars disable Movement SDK and use Final IK against the received root/head/hand targets.

Neither third-party SDK is redistributed by this MIT package. The bridges use Unity components and reflection so the base package still compiles when either SDK is absent. Final IK remains subject to its Unity Asset Store license; Movement SDK remains subject to the Oculus SDK license.

## Requirements

For the current Meta XR Movement SDK release, use Unity 6000.0.66f2 or newer and Meta XR SDK v81 or newer. Install the Meta XR Core SDK and Interaction SDK, then install Movement SDK from:

```text
https://github.com/oculus-samples/Unity-Movement.git
```

Install Final IK from the Unity Asset Store. Add and configure its `VRIK` component on the avatar.

The Taiyo base package retains Unity 2022.3 compatibility. Only projects enabling the current Movement SDK need the newer Unity requirement.

## Avatar prefab setup

1. Import a Humanoid avatar and verify its Avatar definition is valid.
2. Add Meta's body tracking and character retargeting components following the Movement SDK sample.
3. Add Final IK's `VRIK` component to the same avatar and run Final IK's character auto-detection.
4. Select the avatar and run **GameObject > Taiyo Metaverse > Configure Avatar Motion**.
5. On `FinalIkVrikBridge`, assign the HMD/CenterEye target, optional pelvis target, and left/right controller or hand targets.
6. Keep `AvatarMotionPipeline` in `MovementSdkThenFinalIk` mode.
7. Assign `HumanoidPoseSource` to `MetaverseRuntime > Local Pose Source`.

`MovementSdkBridge` auto-detects common Movement/Meta components by type name. If a future SDK version renames them, explicitly assign every body tracking and retargeting `MonoBehaviour` in `Tracking Behaviours`.

## Local and remote ownership

Call this when network ownership changes:

```csharp
motionPipeline.SetLocallyControlled(isLocalPlayer);
```

For the local avatar, Movement SDK and VRIK run in sequence. For a remote avatar, Movement SDK is disabled because remote clients must not read the local headset. VRIK remains enabled and solves the compact root/head/hand pose received by `AvatarRig`.

## Addressables avatar contract

An uploaded avatar prefab should contain:

- a valid Humanoid `Animator`;
- `AvatarRig` with root/head/hand targets used for remote pose playback;
- `FinalIkVrikBridge` pointing at the same targets;
- `AvatarMotionPipeline`;
- optional Movement SDK components for locally controlled avatars.

Do not upload Final IK or Movement SDK source files inside an avatar bundle. Build bundles against SDKs installed in the host Unity project and comply with their respective licenses.
