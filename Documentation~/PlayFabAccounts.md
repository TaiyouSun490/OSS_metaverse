# PlayFab accounts and friends

`PlayFabRestAccountProvider` uses PlayFab Client REST APIs directly. The package therefore does not require the PlayFab Unity SDK and does not pin its version.

## Setup

1. Create a PlayFab title and copy its Title ID.
2. In Unity choose **Create > Taiyo Metaverse > Accounts > PlayFab REST Provider**.
3. Set the Title ID on the new asset.
4. Assign it to `MetaverseConfig > Account Provider`.
5. Sign in or register through `MetaverseRuntime.SignInAsync` / `RegisterAsync` before entering a protected room.

The Title ID is public client configuration. Never store the PlayFab developer secret key in Unity, a ScriptableObject, Addressables, source control, or a player build.

## Sign-in choices

- Email/password and username/password use normal PlayFab client login.
- `RegisterAsync` creates a username/email account.
- `UpgradeGuestAsync` attaches username/password credentials to the current anonymous account without changing its PlayFab ID.
- Guest Custom ID login accepts a server-provisioned ID. Development-only local creation is available through `createGuestAccount=true`.

For new PlayFab titles, anonymous player creation is disabled by default and Microsoft recommends creating anonymous accounts from a trusted server. Keep client guest creation disabled in production.

## Friends

The provider supports:

- list and refresh;
- add by PlayFab ID, title display name, username, or email;
- remove by PlayFab ID;
- friend profile display name and avatar URL when enabled in PlayFab Client Profile Options.

PlayFab Classic Friends uses a direct list-add operation. It is not a two-sided request/accept workflow. For friend requests, create server-authoritative `Pending`, `Accepted`, and `Blocked` records with Azure Functions/CloudScript or PlayFab Entity objects, and only call `AddFriend` after acceptance.

Do not expose email-based friend lookup in a public UI without considering account enumeration and privacy. PlayFab ID or a separate shareable friend code is safer.
