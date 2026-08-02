using System.Collections.Generic;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public interface IPlayerEconomyService
    {
        NetworkList<PlayerEconomyState> PlayerEconomyNet { get; }
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
