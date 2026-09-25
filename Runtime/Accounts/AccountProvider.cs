using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse
{
    public abstract class AccountProvider : ScriptableObject
    {
        public event Action<AccountState> StateChanged;
        public event Action<AccountProfile> ProfileChanged;
        public event Action<IReadOnlyList<FriendProfile>> FriendsChanged;
        public event Action<Exception> Error;

        public abstract AccountState State { get; }
        public abstract AccountProfile CurrentProfile { get; }
        public abstract IReadOnlyList<FriendProfile> Friends { get; }

        public abstract Task InitializeAsync(CancellationToken cancellationToken);
        public abstract Task SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken);
        public abstract Task RegisterAsync(AccountRegistrationRequest request, CancellationToken cancellationToken);
        public abstract Task UpgradeGuestAsync(AccountRegistrationRequest request, CancellationToken cancellationToken);
        public abstract Task SignOutAsync(CancellationToken cancellationToken);
        public abstract Task UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken);
        public abstract Task RefreshFriendsAsync(CancellationToken cancellationToken);
        public abstract Task AddFriendAsync(FriendLookup lookup, CancellationToken cancellationToken);
        public abstract Task RemoveFriendAsync(string accountId, CancellationToken cancellationToken);

        protected void RaiseStateChanged(AccountState state) => StateChanged?.Invoke(state);
        protected void RaiseProfileChanged(AccountProfile profile) => ProfileChanged?.Invoke(profile);
        protected void RaiseFriendsChanged(IReadOnlyList<FriendProfile> friends) => FriendsChanged?.Invoke(friends);
        protected void RaiseError(Exception exception) => Error?.Invoke(exception);
    }
}
