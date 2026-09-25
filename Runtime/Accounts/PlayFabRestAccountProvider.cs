using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Taiyo.Metaverse
{
    /// <summary>
    /// PlayFab Client API adapter implemented over REST, so the metaverse package does not
    /// require or pin a particular PlayFab Unity SDK release.
    /// </summary>
    [CreateAssetMenu(menuName = "Taiyo Metaverse/Accounts/PlayFab REST Provider", fileName = "PlayFabAccountProvider")]
    public sealed class PlayFabRestAccountProvider : AccountProvider
    {
        [SerializeField] private string titleId = string.Empty;
        [SerializeField] private bool requestFriendDisplayNames = true;
        [SerializeField] private bool requestFriendAvatarUrls = true;

        private readonly List<FriendProfile> friends = new List<FriendProfile>();
        private AccountState state = AccountState.Uninitialized;
        private AccountProfile profile;
        private string sessionTicket;

        public override AccountState State => state;
        public override AccountProfile CurrentProfile => profile;
        public override IReadOnlyList<FriendProfile> Friends => friends;

        public override Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateTitleId();
            SetState(AccountState.SignedOut);
            return Task.CompletedTask;
        }

        public override async Task SignInAsync(AccountSignInRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            EnsureInitialized();
            SetState(AccountState.SigningIn);
            try
            {
                LoginData data;
                switch (request.method)
                {
                    case AccountSignInMethod.GuestCustomId:
                        data = await SignInGuestAsync(request, cancellationToken);
                        break;
                    case AccountSignInMethod.EmailPassword:
                        Require(request.identifier, "Email");
                        Require(request.password, "Password");
                        data = await PostAsync<LoginData>("LoginWithEmailAddress", new EmailLoginRequest
                        {
                            TitleId = titleId,
                            Email = request.identifier.Trim(),
                            Password = request.password,
                            InfoRequestParameters = LoginInfoRequest.Create()
                        }, false, cancellationToken);
                        break;
                    case AccountSignInMethod.UsernamePassword:
                        Require(request.identifier, "Username");
                        Require(request.password, "Password");
                        data = await PostAsync<LoginData>("LoginWithPlayFab", new UsernameLoginRequest
                        {
                            TitleId = titleId,
                            Username = request.identifier.Trim(),
                            Password = request.password,
                            InfoRequestParameters = LoginInfoRequest.Create()
                        }, false, cancellationToken);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                ApplyLogin(data, request.method == AccountSignInMethod.GuestCustomId);
                SetState(AccountState.SignedIn);
                RaiseProfileChanged(profile);
                await RefreshFriendsAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                sessionTicket = null;
                SetState(AccountState.Failed);
                RaiseError(exception);
                throw;
            }
        }

        public override async Task RegisterAsync(AccountRegistrationRequest request, CancellationToken cancellationToken)
        {
            ValidateRegistration(request);
            EnsureInitialized();
            SetState(AccountState.SigningIn);
            try
            {
                var data = await PostAsync<RegisterData>("RegisterPlayFabUser", new RegisterRequest
                {
                    TitleId = titleId,
                    Email = EmptyToNull(request.email),
                    Username = EmptyToNull(request.username),
                    Password = request.password,
                    DisplayName = EmptyToNull(request.displayName),
                    RequireBothUsernameAndEmail = false
                }, false, cancellationToken);
                sessionTicket = data.SessionTicket;
                profile = new AccountProfile
                {
                    accountId = data.PlayFabId ?? string.Empty,
                    displayName = request.displayName?.Trim() ?? string.Empty,
                    isGuest = false,
                    createdUtc = DateTime.UtcNow,
                    lastLoginUtc = DateTime.UtcNow
                };
                SetState(AccountState.SignedIn);
                RaiseProfileChanged(profile);
                await RefreshFriendsAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                sessionTicket = null;
                SetState(AccountState.Failed);
                RaiseError(exception);
                throw;
            }
        }

        public override async Task UpgradeGuestAsync(AccountRegistrationRequest request, CancellationToken cancellationToken)
        {
            EnsureSignedIn();
            ValidateRegistration(request);
            Require(request.email, "Email");
            Require(request.username, "Username");
            if (!profile.isGuest)
                throw new InvalidOperationException("The current account is already registered.");

            await PostAsync<EmptyData>("AddUsernamePassword", new AddUsernamePasswordRequest
            {
                Email = EmptyToNull(request.email),
                Username = EmptyToNull(request.username),
                Password = request.password
            }, true, cancellationToken);
            profile.isGuest = false;
            if (!string.IsNullOrWhiteSpace(request.displayName))
                await UpdateDisplayNameAsync(request.displayName, cancellationToken);
            else
                RaiseProfileChanged(profile);
        }

        public override Task SignOutAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sessionTicket = null;
            profile = null;
            friends.Clear();
            SetState(AccountState.SignedOut);
            RaiseProfileChanged(null);
            RaiseFriendsChanged(friends);
            return Task.CompletedTask;
        }

        public override async Task UpdateDisplayNameAsync(string displayName, CancellationToken cancellationToken)
        {
            EnsureSignedIn();
            Require(displayName, "Display name");
            var data = await PostAsync<DisplayNameData>("UpdateUserTitleDisplayName", new DisplayNameRequest
            {
                DisplayName = displayName.Trim()
            }, true, cancellationToken);
            profile.displayName = data.DisplayName ?? displayName.Trim();
            RaiseProfileChanged(profile);
        }

        public override async Task RefreshFriendsAsync(CancellationToken cancellationToken)
        {
            EnsureSignedIn();
            var data = await PostAsync<FriendsData>("GetFriendsList", new FriendsRequest
            {
                ProfileConstraints = new ProfileConstraints
                {
                    ShowDisplayName = requestFriendDisplayNames,
                    ShowAvatarUrl = requestFriendAvatarUrls
                }
            }, true, cancellationToken);

            friends.Clear();
            if (data.Friends != null)
            {
                foreach (var item in data.Friends)
                {
                    if (item == null) continue;
                    friends.Add(new FriendProfile
                    {
                        accountId = FirstNonEmpty(item.FriendPlayFabId, item.Profile?.PlayerId),
                        displayName = FirstNonEmpty(item.TitleDisplayName, item.Profile?.DisplayName),
                        avatarUrl = item.Profile?.AvatarUrl ?? string.Empty,
                        tags = item.Tags ?? Array.Empty<string>(),
                        isOnline = false
                    });
                }
            }
            RaiseFriendsChanged(friends);
        }

        public override async Task AddFriendAsync(FriendLookup lookup, CancellationToken cancellationToken)
        {
            EnsureSignedIn();
            Require(lookup.Value, "Friend identifier");
            var request = new AddFriendRequest();
            switch (lookup.Method)
            {
                case FriendLookupMethod.AccountId: request.FriendPlayFabId = lookup.Value.Trim(); break;
                case FriendLookupMethod.DisplayName: request.FriendTitleDisplayName = lookup.Value.Trim(); break;
                case FriendLookupMethod.Username: request.FriendUsername = lookup.Value.Trim(); break;
                case FriendLookupMethod.Email: request.FriendEmail = lookup.Value.Trim(); break;
                default: throw new ArgumentOutOfRangeException();
            }
            await PostAsync<AddFriendData>("AddFriend", request, true, cancellationToken);
            await RefreshFriendsAsync(cancellationToken);
        }

        public override async Task RemoveFriendAsync(string accountId, CancellationToken cancellationToken)
        {
            EnsureSignedIn();
            Require(accountId, "Friend account ID");
            await PostAsync<EmptyData>("RemoveFriend", new RemoveFriendRequest
            {
                FriendPlayFabId = accountId.Trim()
            }, true, cancellationToken);
            await RefreshFriendsAsync(cancellationToken);
        }

        private async Task<LoginData> SignInGuestAsync(AccountSignInRequest request, CancellationToken cancellationToken)
        {
            var customId = request.identifier?.Trim();
            if (string.IsNullOrEmpty(customId))
            {
                if (!request.createGuestAccount)
                    throw new InvalidOperationException(
                        "A server-provisioned Custom ID is required. Enable client guest creation only for local development.");
                var key = $"Taiyo.Metaverse.PlayFab.{titleId}.CustomId";
                customId = PlayerPrefs.GetString(key, string.Empty);
                if (string.IsNullOrEmpty(customId))
                {
                    customId = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(key, customId);
                    PlayerPrefs.Save();
                }
            }

            return await PostAsync<LoginData>("LoginWithCustomID", new CustomIdLoginRequest
            {
                TitleId = titleId,
                CustomId = customId,
                CreateAccount = request.createGuestAccount,
                InfoRequestParameters = LoginInfoRequest.Create()
            }, false, cancellationToken);
        }

        private void ApplyLogin(LoginData data, bool guest)
        {
            sessionTicket = data.SessionTicket;
            var title = data.InfoResultPayload?.AccountInfo?.TitleInfo;
            profile = new AccountProfile
            {
                accountId = data.PlayFabId ?? string.Empty,
                displayName = title?.DisplayName ?? string.Empty,
                avatarUrl = title?.AvatarUrl ?? string.Empty,
                isGuest = guest,
                createdUtc = ParseDate(title?.Created),
                lastLoginUtc = ParseDate(title?.LastLogin)
            };
        }

        private async Task<T> PostAsync<T>(string operation, object body, bool authenticated, CancellationToken cancellationToken)
            where T : class
        {
            ValidateTitleId();
            if (authenticated) EnsureSignedIn();

            var url = $"https://{titleId}.playfabapi.com/Client/{operation}";
            var json = JsonUtility.ToJson(body);
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            try
            {
                request.SetRequestHeader("Content-Type", "application/json");
                if (authenticated)
                    request.SetRequestHeader("X-Authorization", sessionTicket);
                var operationHandle = request.SendWebRequest();
                while (!operationHandle.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }

                var responseText = request.downloadHandler.text;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var apiError = string.IsNullOrWhiteSpace(responseText)
                        ? null
                        : JsonUtility.FromJson<ApiError>(responseText);
                    throw new PlayFabAccountException(
                        apiError?.error ?? request.error,
                        apiError?.errorMessage ?? responseText,
                        request.responseCode,
                        apiError?.errorCode ?? 0);
                }

                var response = JsonUtility.FromJson<ApiResponse<T>>(responseText);
                if (response == null || response.data == null)
                    throw new PlayFabAccountException("InvalidResponse", "PlayFab returned no response data.", request.responseCode, 0);
                return response.data;
            }
            finally
            {
                request.Dispose();
            }
        }

        private void EnsureInitialized()
        {
            if (state == AccountState.Uninitialized)
                throw new InvalidOperationException("Initialize the account provider before signing in.");
        }

        private void EnsureSignedIn()
        {
            if (state != AccountState.SignedIn || string.IsNullOrWhiteSpace(sessionTicket))
                throw new InvalidOperationException("A signed-in PlayFab session is required.");
        }

        private void ValidateTitleId()
        {
            if (string.IsNullOrWhiteSpace(titleId))
                throw new InvalidOperationException("Set the PlayFab Title ID on the account provider.");
            foreach (var character in titleId)
                if (!char.IsLetterOrDigit(character) && character != '-')
                    throw new InvalidOperationException("PlayFab Title ID contains invalid characters.");
        }

        private static void ValidateRegistration(AccountRegistrationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Require(request.password, "Password");
            if (string.IsNullOrWhiteSpace(request.email) && string.IsNullOrWhiteSpace(request.username))
                throw new ArgumentException("Email or username is required.", nameof(request));
        }

        private static void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(name + " is required.");
        }

        private static string EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string FirstNonEmpty(string first, string second) => !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        private static DateTime ParseDate(string value) => DateTime.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : default;

        private void SetState(AccountState value)
        {
            state = value;
            RaiseStateChanged(value);
        }

        [Serializable] private sealed class ApiResponse<T> { public int code; public string status; public T data; }
        [Serializable] private sealed class ApiError { public int code; public string status; public string error; public int errorCode; public string errorMessage; }
        [Serializable] private sealed class EmptyData { public bool empty; }
        [Serializable] private sealed class LoginInfoRequest { public bool GetUserAccountInfo; public static LoginInfoRequest Create() => new LoginInfoRequest { GetUserAccountInfo = true }; }
        [Serializable] private sealed class CustomIdLoginRequest { public string TitleId; public string CustomId; public bool CreateAccount; public LoginInfoRequest InfoRequestParameters; }
        [Serializable] private sealed class EmailLoginRequest { public string TitleId; public string Email; public string Password; public LoginInfoRequest InfoRequestParameters; }
        [Serializable] private sealed class UsernameLoginRequest { public string TitleId; public string Username; public string Password; public LoginInfoRequest InfoRequestParameters; }
        [Serializable] private sealed class LoginData { public string PlayFabId; public string SessionTicket; public bool NewlyCreated; public InfoPayload InfoResultPayload; }
        [Serializable] private sealed class InfoPayload { public AccountInfo AccountInfo; }
        [Serializable] private sealed class AccountInfo { public TitleInfo TitleInfo; }
        [Serializable] private sealed class TitleInfo { public string DisplayName; public string AvatarUrl; public string Created; public string LastLogin; }
        [Serializable] private sealed class RegisterRequest { public string TitleId; public string Email; public string Username; public string Password; public string DisplayName; public bool RequireBothUsernameAndEmail; }
        [Serializable] private sealed class RegisterData { public string PlayFabId; public string SessionTicket; }
        [Serializable] private sealed class AddUsernamePasswordRequest { public string Email; public string Username; public string Password; }
        [Serializable] private sealed class DisplayNameRequest { public string DisplayName; }
        [Serializable] private sealed class DisplayNameData { public string DisplayName; }
        [Serializable] private sealed class FriendsRequest { public ProfileConstraints ProfileConstraints; }
        [Serializable] private sealed class ProfileConstraints { public bool ShowDisplayName; public bool ShowAvatarUrl; }
        [Serializable] private sealed class FriendsData { public FriendItem[] Friends; }
        [Serializable] private sealed class FriendItem { public string FriendPlayFabId; public string TitleDisplayName; public string[] Tags; public FriendPlayerProfile Profile; }
        [Serializable] private sealed class FriendPlayerProfile { public string PlayerId; public string DisplayName; public string AvatarUrl; }
        [Serializable] private sealed class AddFriendRequest { public string FriendPlayFabId; public string FriendTitleDisplayName; public string FriendUsername; public string FriendEmail; }
        [Serializable] private sealed class AddFriendData { public bool Created; }
        [Serializable] private sealed class RemoveFriendRequest { public string FriendPlayFabId; }
    }

    public sealed class PlayFabAccountException : Exception
    {
        public string ErrorName { get; }
        public long HttpStatus { get; }
        public int PlayFabErrorCode { get; }

        public PlayFabAccountException(string errorName, string message, long httpStatus, int playFabErrorCode)
            : base(string.IsNullOrWhiteSpace(message) ? errorName : message)
        {
            ErrorName = errorName ?? string.Empty;
            HttpStatus = httpStatus;
            PlayFabErrorCode = playFabErrorCode;
        }
    }
}
