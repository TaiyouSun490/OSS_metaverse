# Writing providers

## Networking

Derive a `ScriptableObject` from `NetworkProvider`. Implement initialization, room join/leave, and message sending. Raise peer and message events on Unity's main thread. Channels 0–63 are reserved by the kit; application protocols start at 64.

Providers are cloned by `MetaverseRuntime`, so serialized fields are configuration and non-serialized fields are per-session state. Preserve sender identity when relaying packets and enforce maximum packet and room sizes on both client and server.

## Voice

Derive from `VoiceProvider` for Opus, Vivox, Photon Voice, or another service. Honor `SafetyService.IsMuted` and `IsBlocked` before decoding remote audio. `PcmVoiceProvider` is intentionally simple and is not bandwidth-efficient.

## Authentication

Authentication is deliberately outside the transport abstraction. Obtain a short-lived session token from your backend, configure the selected transport, and only then call `MetaverseRuntime.JoinAsync`. Never trust profile fields sent by clients as authoritative identity or permissions.
