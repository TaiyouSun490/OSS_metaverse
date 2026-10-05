# 0.7 validation

Environment: Unity 6000.5.9f1 on Windows, NGO 2.13.3, Unity Transport 2.6.0. Optional Meta XR Core integration targets 207.0.0. The SDK and research avatar assets are not included in this repository.

Completed checks:

- Fresh consumer project with **no Meta SDK, NGO, AvatarExperimentAssets or VRM dependencies**: imported this UPM package and the Shared Space sample, compiled, ran **19 EditMode tests: 19 passed / 0 failed / 0 skipped**.
- Tests cover rigid coordinate conversion at positive/zero/reverse rotations, length preservation, packet slices/truncation/stale alignment epoch, host authority, separate-space handshake/disconnect, shared-anchor failure/retry/tracking loss/mode switch, and late completion after cancellation. Existing kit tests are included in the count.
- Research app with Meta XR Core 207.0.0: package adapter and application integration compile. Headless play validation passed for reachable panel colliders, mode selection before connecting, disabled Start until peer alignment, colocated failure and separate-space recovery, disabled physical partner repositioning in colocated mode, IP entry/edit/back/rejection, live UDP discovery, all five skin targets, stop/restore and disconnect recovery.
- A minimal NGO consumer was built for Windows and run as three independent headless processes (one host, two clients) over loopback UDP. All three observed two spatially ready peers and received the epoch-wrapped reliable application message from both other peers, including client-to-client relay. All three reported pass and exited. This verifies real NGO transport paths; it does not simulate packet loss or substitute for Quest Wi-Fi testing.

The first attempt to run the headless research scene initialized the installed native OpenXR runtime and crashed because there was no graphics device. The validator was corrected to disable native XR and omit OVRManager in its unsaved test scene; the saved device scene retains XR/OVRManager. A Unity SearchDatabase index exception also appears during the headless scene transition; the explicit application validation subsequently passes. These checks do not test native headset rendering.

Hardware checks still required on **two worn Quest devices**: Meta cloud sharing/loading and recognition, physical registration accuracy across the room, recenter/tracking-loss recovery, stereoscopic skin appearance, controller/hand UI input, performance, and Wi-Fi interruption/rejoin. Mocks and build success are not evidence of these outcomes. The consuming experiment keeps Start gated and provides Stop/retry/separate-space fallback.

Android manifest regression found during the research APK build: Meta SDK 207's late manifest generator removed anchor-sharing and hand-tracking permissions when its separate `OVRProjectConfig` feature settings were left disabled. The consuming build now enables Anchor Support, Shared Anchor Support and the intended hand input mode; its final manifest policy runs after Meta's callback order 99999. The setup guide documents both the settings and inspection of the final APK, rather than treating a successful build as proof that permissions survived.

Neither a Photon account nor a Meta developer app ID is embedded. Group UUIDs are generated per session and sent through the selected transport. A registered app/user-ID entitlement flow is not part of this group-sharing adapter. Release destroys the runtime anchor instance; saved-anchor deletion/reuse UI is not included.
