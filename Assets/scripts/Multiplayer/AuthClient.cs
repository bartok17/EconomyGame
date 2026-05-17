using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace MonopolyGame.Multiplayer
{
    public sealed class AuthClient
    {
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => AuthenticationService.Instance.PlayerId;
        public string DisplayName => AuthenticationService.Instance.PlayerName;

        public async Task SignUpAsync(string username, string password, string displayName)
        {
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(username, password);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                await AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
            }
        }

        public async Task SignInAsync(string username, string password)
        {
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(username, password);
        }

        public Task SetDisplayNameAsync(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", nameof(displayName));
            }

            return AuthenticationService.Instance.UpdatePlayerNameAsync(displayName);
        }

        public void SignOut()
        {
            AuthenticationService.Instance.SignOut();
        }
    }
}
