using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Accounts/Local Account Provider", fileName = "LocalAccountProvider")]
    public sealed class LocalAccountProvider : AccountProvider
    {
        [SerializeField] private string accountId = "local-user";
        [SerializeField] private string displayName = "Local User";

        private static readonly IReadOnlyList<FriendProfile> NoFriends = Array.Empty<FriendProfile>();
        private AccountState state = AccountState.Uninitialized;
        private AccountProfile profile;

        public override AccountState State => state;
        public override AccountProfile CurrentProfile => profile;
        public override IReadOnlyList<FriendProfile> Friends => NoFriends;

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(AccountState.SignedOut);
            return Task.CompletedTask;
        }

        public override Task SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetState(AccountState.SigningIn);
            profile = new AccountProfile
            {
                accountId = string.IsNullOrWhiteSpace(accountId) ? Guid.NewGuid().ToString("N") : accountId,
                displayName = displayName,
                isGuest = true,
                createdUtc = DateTime.UtcNow,
                lastLoginUtc = DateTime.UtcNow
            };
            SetState(AccountState.SignedIn);
            RaiseProfileChanged(profile);
            return Task.CompletedTask;
        }

        public override Task RegisterAsync(AccountRegistrationRequest request, CancellationToken cancellationToken) =>
            SignInAsync(AccountSignInRequest.Guest(), cancellationToken);

        public override Task UpgradeGuestAsync(AccountRegistrationRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (profile != null && !string.IsNullOrWhiteSpace(request?.displayName))
                profile.displayName = request.displayName.Trim();
            RaiseProfileChanged(profile);
            return Task.CompletedTask;
        }

        public override Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            profile = null;
            SetState(AccountState.SignedOut);
            RaiseFriendsChanged(NoFriends);
            return Task.CompletedTask;
        }

        public override Task UpdateDisplayNameAsync(string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (profile == null) throw new InvalidOperationException("Sign in first.");
            profile.displayName = value?.Trim() ?? string.Empty;
            RaiseProfileChanged(profile);
            return Task.CompletedTask;
        }

        public override Task RefreshFriendsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RaiseFriendsChanged(NoFriends);
            return Task.CompletedTask;
        }

        public override Task AddFriendAsync(FriendLookup lookup, CancellationToken cancellationToken) =>
            throw new NotSupportedException("LocalAccountProvider does not persist friends.");

        public override Task RemoveFriendAsync(string friendAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("LocalAccountProvider does not persist friends.");

        private void SetState(AccountState value)
        {
            state = value;
            RaiseStateChanged(value);
        }
    }
}
