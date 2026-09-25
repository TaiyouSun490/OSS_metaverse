using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Taiyo.Metaverse.Samples
{
    /// <summary>Small IMGUI harness for exercising account and friend APIs in the sample.</summary>
    public sealed class AccountFriendsHud : MonoBehaviour
    {
        [SerializeField] private MetaverseRuntime runtime;
        [SerializeField] private bool allowDevelopmentGuestCreation;

        private string identifier = string.Empty;
        private string password = string.Empty;
        private string displayName = string.Empty;
        private string friendIdentifier = string.Empty;
        private string status = "Signed out";
        private bool busy;

        private void Start()
        {
            if (runtime == null)
                runtime = FindObjectOfType<MetaverseRuntime>();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 376, 16, 360, Mathf.Min(620, Screen.height - 32)), GUI.skin.box);
            GUILayout.Label("Account & Friends");
            GUILayout.Label(status);

            var account = runtime != null ? runtime.Account : null;
            if (account == null)
            {
                GUILayout.Label("Assign an AccountProvider to MetaverseConfig.");
                GUILayout.EndArea();
                return;
            }

            if (account.State != AccountState.SignedIn)
            {
                GUILayout.Label("Email or username");
                identifier = GUILayout.TextField(identifier);
                GUILayout.Label("Password");
                password = GUILayout.PasswordField(password, '*');
                GUILayout.Label("Display name (registration)");
                displayName = GUILayout.TextField(displayName);

                GUI.enabled = !busy;
                if (GUILayout.Button("Sign in with email"))
                    Run(() => runtime.SignInAsync(new AccountSignInRequest
                    {
                        method = AccountSignInMethod.EmailPassword,
                        identifier = identifier,
                        password = password
                    }));
                if (GUILayout.Button("Register"))
                    Run(() => runtime.RegisterAsync(new AccountRegistrationRequest
                    {
                        email = identifier,
                        password = password,
                        displayName = displayName
                    }));
                if (allowDevelopmentGuestCreation && GUILayout.Button("Development guest"))
                    Run(() => runtime.SignInAsync(AccountSignInRequest.Guest(null, true)));
                GUI.enabled = true;
            }
            else
            {
                var profile = account.CurrentProfile;
                GUILayout.Label($"{profile?.displayName} ({profile?.accountId})");
                GUILayout.Label(profile != null && profile.isGuest ? "Guest account" : "Registered account");
                GUILayout.Space(6f);

                GUILayout.Label("Friend PlayFab ID / display name");
                friendIdentifier = GUILayout.TextField(friendIdentifier);
                GUI.enabled = !busy;
                if (GUILayout.Button("Add by PlayFab ID"))
                    Run(() => account.AddFriendAsync(
                        new FriendLookup(FriendLookupMethod.AccountId, friendIdentifier),
                        destroyCancellationToken));
                if (GUILayout.Button("Add by display name"))
                    Run(() => account.AddFriendAsync(
                        new FriendLookup(FriendLookupMethod.DisplayName, friendIdentifier),
                        destroyCancellationToken));
                if (GUILayout.Button("Refresh friends"))
                    Run(() => account.RefreshFriendsAsync(destroyCancellationToken));
                GUI.enabled = true;

                GUILayout.Label($"Friends ({account.Friends.Count})");
                foreach (var friend in account.Friends)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(string.IsNullOrEmpty(friend.displayName) ? friend.accountId : friend.displayName);
                    if (!busy && GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        var id = friend.accountId;
                        Run(() => account.RemoveFriendAsync(id, destroyCancellationToken));
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndArea();
        }

        private async void Run(Func<Task> action)
        {
            busy = true;
            status = "Working…";
            try
            {
                await action();
                status = "Done";
            }
            catch (Exception exception)
            {
                status = exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                busy = false;
            }
        }
    }
}
