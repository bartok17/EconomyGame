using System.Collections.Generic;

namespace MonopolyGame.Multiplayer
{
    public interface IRememberedAccountStore
    {
        IReadOnlyList<RememberedAccount> LoadAll();
        void SaveAll(IReadOnlyList<RememberedAccount> accounts);
    }
}
