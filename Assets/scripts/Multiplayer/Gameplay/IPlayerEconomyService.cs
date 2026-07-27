using System.Collections.Generic;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Manages player economy state: balance, property ownership, network sync.
    /// </summary>
    public interface IPlayerEconomyService
    {
        /// <summary>NetworkList exposed for change-tracking by the session controller.</summary>
        NetworkList<PlayerEconomyState> PlayerEconomyNet { get; }
        /// <summary>NetworkList exposed for ownership change-tracking by the session controller.</summary>
        NetworkList<PropertyOwnershipState> PropertyOwnershipNet { get; }

        int GetBalance(int pawnSlot);
        void SetBalance(int pawnSlot, int newBalance);
        void AddBalance(int pawnSlot, int amount);
        bool DeductBalance(int pawnSlot, int amount);

        void SetPropertyOwner(int spaceIndex, int ownerPawnSlot, string ownerPlayerId, string ownerName);
        int GetPropertyOwnerPawnSlot(int spaceIndex);
        string GetPropertyOwnerName(int spaceIndex);
        bool IsPropertyOwned(int spaceIndex);

        IReadOnlyList<PlayerEconomyState> GetAllEconomyStates();
        IReadOnlyList<PropertyOwnershipState> GetAllPropertyOwnerships();

        void Initialize(int startingBalance);
        void Clear();
        
        void AddPlayerState(PlayerEconomyState state);
        void AddPropertyState(PropertyOwnershipState state);
        PlayerEconomyState GetPlayerState(int pawnSlot);
        void UpdatePlayerState(int pawnSlot, PlayerEconomyState newState);
        void UpdatePropertyState(int spaceIndex, PropertyOwnershipState newState);
    }
}
