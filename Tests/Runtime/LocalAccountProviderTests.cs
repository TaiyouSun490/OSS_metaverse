using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Taiyo.Metaverse.Tests
{
    public sealed class LocalAccountProviderTests
    {
        [Test]
        public async Task SignInAndSignOut_UpdatesStateAndProfile()
        {
            var provider = ScriptableObject.CreateInstance<LocalAccountProvider>();
            try
            {
                await provider.InitializeAsync(CancellationToken.None);
                Assert.That(provider.State, Is.EqualTo(AccountState.SignedOut));

                await provider.SignInAsync(AccountSignInRequest.Guest(), CancellationToken.None);
                Assert.That(provider.State, Is.EqualTo(AccountState.SignedIn));
                Assert.That(provider.CurrentProfile.accountId, Is.Not.Empty);

                await provider.SignOutAsync(CancellationToken.None);
                Assert.That(provider.State, Is.EqualTo(AccountState.SignedOut));
                Assert.That(provider.CurrentProfile, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(provider);
            }
        }
    }
}
