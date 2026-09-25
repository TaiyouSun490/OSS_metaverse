using System;
using System.Collections.Generic;

namespace Taiyo.Metaverse
{
    public enum AccountState
    {
        Uninitialized,
        SignedOut,
        SigningIn,
        SignedIn,
        Failed
    }

    public enum AccountSignInMethod
    {
        GuestCustomId,
        EmailPassword,
        UsernamePassword
    }

    public enum FriendLookupMethod
    {
        AccountId,
        DisplayName,
        Username,
        Email
    }

    [Serializable]
    public sealed class AccountSignInRequest
    {
        public AccountSignInMethod method = AccountSignInMethod.GuestCustomId;
        public string identifier = string.Empty;
        public string password = string.Empty;
        public bool createGuestAccount;

        public static AccountSignInRequest Guest(string customId = null, bool createAccount = false) =>
            new AccountSignInRequest
            {
                method = AccountSignInMethod.GuestCustomId,
                identifier = customId ?? string.Empty,
                createGuestAccount = createAccount
            };
    }

    [Serializable]
    public sealed class AccountRegistrationRequest
    {
        public string email = string.Empty;
        public string username = string.Empty;
        public string password = string.Empty;
        public string displayName = string.Empty;
    }

    [Serializable]
    public sealed class AccountProfile
    {
        public string accountId = string.Empty;
        public string displayName = string.Empty;
        public string avatarUrl = string.Empty;
        public bool isGuest;
        public DateTime createdUtc;
        public DateTime lastLoginUtc;
    }

    [Serializable]
    public sealed class FriendProfile
    {
        public string accountId = string.Empty;
        public string displayName = string.Empty;
        public string avatarUrl = string.Empty;
        public bool isOnline;
        public IReadOnlyList<string> tags = Array.Empty<string>();
    }

    public readonly struct FriendLookup
    {
        public readonly FriendLookupMethod Method;
        public readonly string Value;

        public FriendLookup(FriendLookupMethod method, string value)
        {
            Method = method;
            Value = value ?? string.Empty;
        }
    }
}
