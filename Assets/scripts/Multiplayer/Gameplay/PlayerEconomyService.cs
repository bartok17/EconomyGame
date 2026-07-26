using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Server-authoritative player economy manager.
    /// Owns the NetworkLists and exposes balance/property read/write operations.
    /// </summary>
    public sealed class PlayerEconomyService : IPlayerEconomyService
    {
        private readonly NetworkList<PlayerEconomyState> _playerEconomyNet;
        private readonly NetworkList<PropertyOwnershipState> _propertyOwnershipNet;

        public NetworkList<PlayerEconomyState> PlayerEconomyNet => _playerEconomyNet;
        public NetworkList<PropertyOwnershipState> PropertyOwnershipNet => _propertyOwnershipNet;

        public PlayerEconomyService(NetworkList<PlayerEconomyState> playerEconomyNet, NetworkList<PropertyOwnershipState> propertyOwnershipNet)
        {
            _playerEconomyNet = playerEconomyNet ?? throw new ArgumentNullException(nameof(playerEconomyNet));
            _propertyOwnershipNet = propertyOwnershipNet ?? throw new ArgumentNullException(nameof(propertyOwnershipNet));
        }

        public int GetBalance(int pawnSlot)
        {
            for (int i = 0; i < _playerEconomyNet.Count; i++)
            {
                if (_playerEconomyNet[i].PawnSlot == pawnSlot)
                {
                    return _playerEconomyNet[i].Balance;
                }
            }

            return 0;
        }

        public void SetBalance(int pawnSlot, int newBalance)
        {
            for (int i = 0; i < _playerEconomyNet.Count; i++)
            {
                if (_playerEconomyNet[i].PawnSlot == pawnSlot)
                {
                    _playerEconomyNet[i] = _playerEconomyNet[i].WithBalance(newBalance);
                    return;
                }
            }
        }

        public void AddBalance(int pawnSlot, int amount)
        {
            int current = GetBalance(pawnSlot);
            SetBalance(pawnSlot, current + amount);
        }

        public bool DeductBalance(int pawnSlot, int amount)
        {
            int current = GetBalance(pawnSlot);

            if (current < amount)
            {
                return false;
            }

            SetBalance(pawnSlot, current - amount);
            return true;
        }

        public void SetPropertyOwner(int spaceIndex, int ownerPawnSlot, string ownerPlayerId, string ownerName)
        {
            for (int i = 0; i < _propertyOwnershipNet.Count; i++)
            {
                if (_propertyOwnershipNet[i].SpaceIndex == spaceIndex)
                {
                    _propertyOwnershipNet[i] = new PropertyOwnershipState(spaceIndex, ownerPawnSlot, ownerPlayerId, ownerName);
                    return;
                }
            }

            _propertyOwnershipNet.Add(new PropertyOwnershipState(spaceIndex, ownerPawnSlot, ownerPlayerId, ownerName));
        }

        public int GetPropertyOwnerPawnSlot(int spaceIndex)
        {
            for (int i = 0; i < _propertyOwnershipNet.Count; i++)
            {
                if (_propertyOwnershipNet[i].SpaceIndex == spaceIndex)
                {
                    return _propertyOwnershipNet[i].OwnerPawnSlot;
                }
            }

            return -1;
        }

        public bool IsPropertyOwned(int spaceIndex)
        {
            return GetPropertyOwnerPawnSlot(spaceIndex) >= 0;
        }

        public IReadOnlyList<PlayerEconomyState> GetAllEconomyStates()
        {
            var list = new List<PlayerEconomyState>(_playerEconomyNet.Count);

            for (int i = 0; i < _playerEconomyNet.Count; i++)
            {
                list.Add(_playerEconomyNet[i]);
            }

            return list;
        }

        public IReadOnlyList<PropertyOwnershipState> GetAllPropertyOwnerships()
        {
            var list = new List<PropertyOwnershipState>(_propertyOwnershipNet.Count);

            for (int i = 0; i < _propertyOwnershipNet.Count; i++)
            {
                list.Add(_propertyOwnershipNet[i]);
            }

            return list;
        }

        public void Initialize(int startingBalance)
        {
            _playerEconomyNet.Clear();
            _propertyOwnershipNet.Clear();
        }

        public void Clear()
        {
            _playerEconomyNet.Clear();
            _propertyOwnershipNet.Clear();
        }
    }
}
