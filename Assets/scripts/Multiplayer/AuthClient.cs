using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;

namespace MonopolyGame.Multiplayer
{
    public sealed class AuthClient : IAuthClient
    {
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => AuthenticationService.Instance.PlayerId;
        public string DisplayName => GetEffectiveDisplayName();

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

        public void SetProfile(string profile)
        {
            AuthenticationService.Instance.SwitchProfile(profile);
        }

        public bool HasCachedSession(string profile)
        {
            SetProfile(profile);
            return AuthenticationService.Instance.SessionTokenExists;
        }

        public async Task<bool> TryRestoreSessionAsync(string profile)
        {
            SetProfile(profile);

            // Never call SignInAnonymouslyAsync without a token: the SDK would
            // silently create a new anonymous account instead of failing.
            if (!AuthenticationService.Instance.SessionTokenExists)
            {
                return false;
            }

            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void ClearSession(string profile)
        {
            SetProfile(profile);
            AuthenticationService.Instance.ClearSessionToken();
        }

        private string GetEffectiveDisplayName()
        {
            var playerName = AuthenticationService.Instance.PlayerName;
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                return playerName;
            }

            var playerId = AuthenticationService.Instance.PlayerId;
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                var suffixLength = Math.Min(6, playerId.Length);
                return $"Player-{playerId.Substring(0, suffixLength)}";
            }

            return "Player";
        }
    }
}
