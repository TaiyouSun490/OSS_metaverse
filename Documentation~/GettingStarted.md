# Getting started

## Installation

Add this folder as a local package from Unity Package Manager, or add it to `Packages/manifest.json`:

```json
"com.taiyo.metaverse": "file:../com.taiyo.metaverse"
```

Unity 2022.3 LTS or later and Addressables are required.

## First scene

1. Choose **Tools > Taiyo Metaverse > Create Bootstrap**.
2. Add a desktop rig or an XR Origin. The kit does not force a particular XR rig package.
3. Add `TransformPoseSource` and wire its head and hand transforms.
4. Assign the pose source to `MetaverseRuntime`.
5. Press Play. The generated configuration joins the `lobby` Loopback room.

Loopback is deterministic and dependency-free, intended for scene and protocol development. Use NGO for an OSS-first deployment or Photon Fusion 2.1 for Photon Cloud. PUN 2 is retained only for existing-project compatibility.

## Addressables content

- Mark avatar prefabs and world scenes Addressable.
- Each avatar prefab should have an `AvatarRig` with root/head/hand targets.
- Set the user's `avatarAddress` to the prefab address.
- Set Addressables Remote Build/Load paths for your CDN.
- Create **Taiyo Metaverse > Content Publish Profile** and open **Tools > Taiyo Metaverse > Content Publisher**.

HTTP uploads use PUT. Configure your storage/CDN to accept PUT per object, or implement a publisher for its signed-upload API. Never put credentials in the Unity asset; the built-in publisher reads a bearer token from an environment variable.
