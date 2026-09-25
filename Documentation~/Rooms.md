# Public and private rooms

Every `RoomRequest` has a `RoomVisibility`:

- `Public`: may appear in `DiscoverPublicRoomsAsync` results.
- `Private`: hidden from discovery and joined by an exact room ID plus a short-lived access token.

```csharp
var rooms = await runtime.DiscoverPublicRoomsAsync();

await runtime.JoinAsync(new RoomRequest
{
    roomId = "friends-2026",
    visibility = RoomVisibility.Private,
    createIfMissing = false,
    accessToken = inviteToken
});
```

## Security boundary

Hiding a room does not authorize it. Room IDs, Photon room properties, and client-side token comparisons are not security boundaries.

For production private rooms:

1. The owner creates a room through a trusted PlayFab Function.
2. The backend stores owner/member/friend permissions and a room expiry.
3. An invited user requests access with their PlayFab entity/session identity.
4. The backend returns a short-lived, single-room join token.
5. NGO Connection Approval, a Photon WebHook/plugin, or the authoritative room server validates that token before accepting the player.

Never use an account password as `RoomRequest.accessToken`. The built-in Loopback token check exists for local protocol and UI testing only.

Photon Fusion maps visibility to `StartGameArgs.IsVisible`, while Photon PUN maps it to `RoomOptions.IsVisible`; private rooms do not appear in lobby results. NGO has no built-in room directory, so discovery and token validation belong in PlayFab plus NGO Connection Approval. Visibility never replaces backend or authoritative-server token validation.
