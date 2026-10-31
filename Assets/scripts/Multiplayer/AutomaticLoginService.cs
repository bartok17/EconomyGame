using System;
using System.Collections.Generic;
using System.Linq;

namespace MonopolyGame.Multiplayer
{
    /// <summary>
    /// Owns the remembered-account list: loading, recording successful sign-ins,
    /// touch/forget operations and persistence. Does not perform sign-in itself —
    /// the MultiplayerFlowCoordinator drives the auth state machine.
    /// </summary>
    public sealed class AutomaticLoginService : IAutomaticLoginService
    {
        public const int MaxRememberedAccounts = 5;

        private readonly IRememberedAccountStore _store;
        private readonly IAuthClient _authClient;
        private readonly List<RememberedAccount> _accounts = new List<RememberedAccount>();
        private bool _loaded;

        public IReadOnlyList<RememberedAccount> Accounts => _accounts;

        public event Action<IReadOnlyList<RememberedAccount>> AccountsChanged;

        public AutomaticLoginService(IRememberedAccountStore store, IAuthClient authClient)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
        }

        public void Load()
        {
            _accounts.Clear();
            _accounts.AddRange(_store.LoadAll());
            SortByLastUsed();
            _loaded = true;
        }

        public void RecordSuccessfulSignIn(string username, string displayName)
        {
            EnsureLoaded();

            var id = AccountProfile.FromUsername(username);
            var existing = _accounts.FirstOrDefault(account => account.id == id);
            if (existing != null)
            {
                _accounts.Remove(existing);
            }

            _accounts.Insert(0, new RememberedAccount(
                id,
                username,
                string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
                DateTime.UtcNow.Ticks));

            EnforceCap();
            PersistAndNotify();
        }

        public void Touch(string accountId)
        {
            EnsureLoaded();

            var existing = _accounts.FirstOrDefault(account => account.id == accountId);
            if (existing == null)
            {
                return;
            }

            existing.lastUsedTicks = DateTime.UtcNow.Ticks;
            PersistAndNotify();
        }

        public void Forget(string accountId)
        {
            EnsureLoaded();

            var existing = _accounts.FirstOrDefault(account => account.id == accountId);
            if (existing == null)
            {
                return;
            }

            _accounts.Remove(existing);
            PersistAndNotify();
            TryClearSession(existing.id);
        }

        private void TryClearSession(string profile)
        {
            // The SDK only allows profile switching / token clearing while signed out.
            // Forget is only reachable from the signed-out auth panel, but guard anyway.
            if (_authClient.IsSignedIn)
            {
                return;
            }

            try
            {
                _authClient.ClearSession(profile);
            }
            catch (Exception)
            {
                // Best effort: the entry is already gone and a stale token is harmless.
            }
        }

        private void EnforceCap()
        {
            if (_accounts.Count > MaxRememberedAccounts)
            {
                _accounts.RemoveRange(MaxRememberedAccounts, _accounts.Count - MaxRememberedAccounts);
            }
        }

        private void SortByLastUsed()
        {
            _accounts.Sort((a, b) => b.lastUsedTicks.CompareTo(a.lastUsedTicks));
        }

        private void PersistAndNotify()
        {
            SortByLastUsed();
            _store.SaveAll(_accounts);
            AccountsChanged?.Invoke(_accounts);
        }

        private void EnsureLoaded()
        {
            if (!_loaded)
            {
                Load();
            }
        }
    }
}
