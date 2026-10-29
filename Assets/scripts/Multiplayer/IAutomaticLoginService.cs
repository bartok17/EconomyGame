using System;
using System.Collections.Generic;

namespace MonopolyGame.Multiplayer
{
    public interface IAutomaticLoginService
    {
        IReadOnlyList<RememberedAccount> Accounts { get; }

        event Action<IReadOnlyList<RememberedAccount>> AccountsChanged;

        void Load();
        void RecordSuccessfulSignIn(string username, string displayName);
        void Touch(string accountId);
        void Forget(string accountId);
    }
}
