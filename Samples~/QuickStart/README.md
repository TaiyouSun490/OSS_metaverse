# Quick Start sample

1. Run **Tools > Taiyo Metaverse > Create Bootstrap**.
2. Add a desktop rig or XR Origin to the scene.
3. Add `TransformPoseSource` and assign root, head, and optional hand transforms.
4. Assign that component to `Metaverse Runtime > Local Pose Source`.
5. Add `QuickStartHud` to any GameObject and enter Play mode.

Duplicate the project or run a second editor instance to test Loopback in one process. For separate processes, select the Netcode or Photon provider described in the package README.

For a body-tracked avatar, install Meta XR Movement SDK and Final IK, select the Humanoid avatar, and run **GameObject > Taiyo Metaverse > Configure Avatar Motion**. Assign the HMD and hand targets on `FinalIkVrikBridge`, then use the generated `HumanoidPoseSource` as the runtime's local pose source.
