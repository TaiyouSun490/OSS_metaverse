# Shared Space controls

Use with the normal kit bootstrap and your chosen configured transport. This sample contains no avatar assets or vendor SDK.

1. Import this sample through Package Manager. Select the bootstrap's `MetaverseRuntime` and run **GameObject > Taiyo Metaverse > Add Shared Space**.
2. Turn off `Join Default Room On Start`. Assign your world-space `TrackedPoseSource` as usual.
3. Add `SharedSpaceControls`; assign Runtime, Space and Head. Wire desktop/XR UI buttons to `HostSameRoom`, `HostSeparateSpaces`, `JoinHost`, `RetryAlignment`, `SwitchToSeparateSpaces`, and `Disconnect`. Display `space.Status` and `controls.Status` in your UI.
4. Configure the transport's address/Relay/session settings before pressing Host/Join. `hostPeerId` must be your session authority's actual peer id (NGO's server is `0`; do not use that default for Loopback/Photon).
5. For separate spaces, place each client's `Local Virtual Origin` where the common virtual room origin should be in that client's world. This allows different starting positions without resizing or moving the XR tracking rig. No Meta SDK/services required.
6. For same room, follow [SharedSpaces.md](../../Documentation~/SharedSpaces.md) to install/configure Meta XR Core and assign `MetaSharedAnchorProvider` on each device. The host publishes a new anchor; other devices localize it through the group id received over your transport.

Only enable body/tool interactions while `space.IsPeerReady(peer)` is true. Add an independent explicit Start/Stop control for experiments. Mode changes and retries require fresh consent in the consuming application's workflow. A green alignment status describes localization, not measured registration accuracy.
