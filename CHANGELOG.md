# Changelog

## 0.6.0 - 2026-09-25

- Added an optional Photon Fusion 2.1 network provider with Shared and Host/Client topologies.
- Added public/private Fusion session configuration and reliable/unreliable packet transport.
- Added an optional Photon Voice for Fusion provider with Opus voice, encryption, input mute, and safety mute/block integration.
- Kept PUN 2 as a legacy compatibility adapter and documented NGO/Fusion as the new-project choices.
- Kept all third-party SDKs out of the repository; integrations compile only when explicitly enabled.

## 0.5.0 - 2026-08-13

- Added calibrated SteamVR waist and foot tracker FBT input.
- Added a mocopi-driven Humanoid source for standalone Android/Quest builds.
- Added saved tracker-to-avatar position and rotation calibration profiles.
- Added automatic pelvis and foot target setup plus a calibration HUD.
- Extended the network pose with hips and feet while retaining legacy packet decoding.
- Added Final IK VRIK leg target binding and FBT documentation.

## 0.4.0 - 2026-08-13

- Added an optional Meta XR Movement SDK body-tracking bridge.
- Added a reflection-based Final IK VRIK target bridge.
- Added local/remote avatar motion ownership switching.
- Added a Humanoid pose source for transmitting the final retargeted pose.
- Added an avatar motion setup menu, tests, and integration documentation.

## 0.3.0 - 2026-08-13

- Added provider-neutral Public and Private room visibility.
- Added public room discovery with private room filtering.
- Added private room access tokens for local protocol testing.
- Mapped Photon PUN visibility to `RoomOptions.IsVisible`.
- Added a room browser sample and production authorization guidance.

## 0.2.0 - 2026-08-13

- Added provider-neutral accounts and friend list APIs.
- Added PlayFab Client REST authentication without a Unity SDK dependency.
- Added registration, email/username login, development guest login, and guest account upgrade.
- Added PlayFab friend list, add/remove, profile display name, and avatar URL support.
- Linked signed-in account identity to metaverse presence.

## 0.1.0 - 2026-08-13

- Initial OSS foundation.
- Provider-neutral rooms, presence, pose replication, safety controls, and metrics.
- Loopback, Netcode for GameObjects, and Photon PUN networking adapters.
- Built-in development PCM voice and an extensible voice provider API.
- Desktop/XR pose sources and Addressables avatar/world delivery.
- Addressables build and local-folder/HTTP PUT publishing tools.
