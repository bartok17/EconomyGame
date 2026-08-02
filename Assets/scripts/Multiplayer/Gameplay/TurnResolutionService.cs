using System;
using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Pawns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class TurnResolutionService
    {
        private readonly IPlayerEconomyService _economy;
        private readonly NetworkVariable<int> _pendingPurchaseSpaceIndexNet;
        private readonly NetworkVariable<int> _lastResolvedSpaceIndexNet;
        private readonly NetworkVariable<FixedString128Bytes> _lastEconomyMessageNet;
        private readonly int _passStartReward;
        private readonly int _jailSpaceIndex;
        private readonly float _pawnMoveDuration;
        private readonly Action _onEconomyChanged;

        public TurnResolutionService(
            IPlayerEconomyService economy,
            NetworkVariable<int> pendingPurchaseSpaceIndexNet,
            NetworkVariable<int> lastResolvedSpaceIndexNet,
            NetworkVariable<FixedString128Bytes> lastEconomyMessageNet,
            int passStartReward,
            int jailSpaceIndex,
            float pawnMoveDuration,
            Action onEconomyChanged)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _pendingPurchaseSpaceIndexNet = pendingPurchaseSpaceIndexNet ?? throw new ArgumentNullException(nameof(pendingPurchaseSpaceIndexNet));
            _lastResolvedSpaceIndexNet = lastResolvedSpaceIndexNet ?? throw new ArgumentNullException(nameof(lastResolvedSpaceIndexNet));
            _lastEconomyMessageNet = lastEconomyMessageNet ?? throw new ArgumentNullException(nameof(lastEconomyMessageNet));
            _passStartReward = passStartReward;
            _jailSpaceIndex = jailSpaceIndex;
            _pawnMoveDuration = pawnMoveDuration;
            _onEconomyChanged = onEconomyChanged;
        }

        // ── Initialization ──────────────────────────────────────────────────────────

        public void InitializeEconomyState(IReadOnlyList<PlayerPawnNetworkSync> pawns, int spaceCount, int startingBalance)
        {
            _economy.Clear();

            for (int i = 0; i < pawns.Count; i++)
            {
                PlayerPawnNetworkSync pawn = pawns[i];
                if (pawn != null)
                {
                    _economy.AddPlayerState(new PlayerEconomyState(pawn.PawnSlot, pawn.PlayerId, pawn.DisplayName, startingBalance));
                }
            }

            for (int i = 0; i < spaceCount; i++)
            {
                _economy.AddPropertyState(new PropertyOwnershipState(i, -1, string.Empty, string.Empty));
            }

            _pendingPurchaseSpaceIndexNet.Value = -1;
        }

        // ── Resolution steps (called by host-only coroutine in the controller) ──────

        public void ResolvePassStartReward(PlayerPawnNetworkSync pawn, bool passedStart)
        {
            if (pawn == null || !passedStart || _passStartReward <= 0)
            {
                return;
            }

            _economy.AddBalance(pawn.PawnSlot, _passStartReward);

            PlayerEconomyState state = _economy.GetPlayerState(pawn.PawnSlot);
            SetEconomyMessage($"{state.DisplayName} received {_passStartReward} for passing Start.");
            _onEconomyChanged?.Invoke();
        }

        public bool ResolveGoToJail(
            PlayerPawnNetworkSync pawn,
            BoardLandingResult result,
            BoardManager boardManager,
            Action<int, int, float> movePawnRpc)
        {
            if (pawn == null || result == null || result.SpaceType != BoardSpaceType.GoToJail)
            {
                return false;
            }

            int jailIndex = FindFirstSpaceIndexByType(boardManager, BoardSpaceType.Jail, _jailSpaceIndex);

            _pendingPurchaseSpaceIndexNet.Value = -1;
            _lastResolvedSpaceIndexNet.Value = jailIndex;

            movePawnRpc?.Invoke(pawn.PawnSlot, jailIndex, _pawnMoveDuration);

            SetEconomyMessage($"{pawn.DisplayName} was sent to Jail.");
            _onEconomyChanged?.Invoke();

            return true;
        }

        public void ResolveTaxPayment(PlayerPawnNetworkSync pawn, BoardLandingResult result)
        {
            if (pawn == null || result == null || result.SpaceType != BoardSpaceType.Tax || result.Price <= 0)
            {
                return;
            }

            PlayerEconomyState state = _economy.GetPlayerState(pawn.PawnSlot);
            int taxToPay = Mathf.Min(result.Price, state.Balance);

            if (taxToPay <= 0)
            {
                return;
            }

            _economy.DeductBalance(pawn.PawnSlot, taxToPay);

            SetEconomyMessage($"{state.DisplayName} paid {taxToPay} tax on {result.DisplayName}.");
            Debug.Log($"[Economy] {state.DisplayName} paid {taxToPay} tax on {result.DisplayName}.");

            _onEconomyChanged?.Invoke();
        }

        public void ResolveAutomaticRent(PlayerPawnNetworkSync pawn, BoardLandingResult result)
        {
            if (pawn == null || result == null || result.SpaceType != BoardSpaceType.Property || result.BaseRent <= 0)
            {
                return;
            }

            int ownerSlot = _economy.GetPropertyOwnerPawnSlot(result.SpaceIndex);
            if (ownerSlot < 0 || ownerSlot == pawn.PawnSlot)
            {
                return;
            }

            PlayerEconomyState payer = _economy.GetPlayerState(pawn.PawnSlot);
            PlayerEconomyState owner = _economy.GetPlayerState(ownerSlot);

            int rentToPay = Mathf.Min(result.BaseRent, payer.Balance);
            if (rentToPay <= 0)
            {
                return;
            }

            _economy.DeductBalance(pawn.PawnSlot, rentToPay);
            _economy.AddBalance(ownerSlot, rentToPay);

            SetEconomyMessage($"{payer.DisplayName} paid {rentToPay} rent to {owner.DisplayName} for {result.DisplayName}.");
            Debug.Log($"[Economy] {payer.DisplayName} paid {rentToPay} rent to {owner.DisplayName} for {result.DisplayName}.");

            _onEconomyChanged?.Invoke();
        }

        public void ResolveSpecialSpaceMessage(BoardLandingResult result)
        {
            if (result == null)
            {
                return;
            }

            switch (result.SpaceType)
            {
                case BoardSpaceType.Jail:
                    SetEconomyMessage($"{result.DisplayName}: just visiting.");
                    break;

                case BoardSpaceType.Parking:
                    SetEconomyMessage($"{result.DisplayName}: no action.");
                    break;

                case BoardSpaceType.ActionCard:
                    SetEconomyMessage($"{result.DisplayName}: action cards are not implemented yet.");
                    break;

                case BoardSpaceType.EventCard:
                    SetEconomyMessage($"{result.DisplayName}: event cards are not implemented yet.");
                    break;
            }
        }

        public void UpdatePendingPurchase(BoardLandingResult result, BoardManager boardManager)
        {
            _pendingPurchaseSpaceIndexNet.Value = -1;

            if (result == null || result.SpaceType != BoardSpaceType.Property || result.Price <= 0)
            {
                return;
            }

            if (!_economy.IsPropertyOwned(result.SpaceIndex))
            {
                _pendingPurchaseSpaceIndexNet.Value = result.SpaceIndex;
            }
        }

        // ── Purchase ────────────────────────────────────────────────────────────────

        public bool TryBuyProperty(int spaceIndex, PlayerPawnNetworkSync pawn, BoardManager boardManager)
        {
            if (spaceIndex < 0 || pawn == null || boardManager == null)
            {
                return false;
            }

            BoardState boardState = boardManager.CaptureBoardState();
            BoardSpaceSnapshot space = boardState.GetSpace(spaceIndex);
            if (space == null || space.SpaceType != BoardSpaceType.Property)
            {
                return false;
            }

            if (_economy.GetPropertyOwnerPawnSlot(spaceIndex) >= 0)
            {
                return false;
            }

            PlayerEconomyState economy = _economy.GetPlayerState(pawn.PawnSlot);
            if (economy.Balance < space.Price)
            {
                return false;
            }

            _economy.UpdatePlayerState(pawn.PawnSlot, economy.WithBalance(economy.Balance - space.Price));
            _economy.UpdatePropertyState(spaceIndex, new PropertyOwnershipState(spaceIndex, pawn.PawnSlot, pawn.PlayerId, pawn.DisplayName));

            boardManager.SetSpaceOwner(spaceIndex, pawn.PlayerId);
            _pendingPurchaseSpaceIndexNet.Value = -1;

            SetEconomyMessage($"{pawn.DisplayName} bought {space.DisplayName} for {space.Price}.");
            _onEconomyChanged?.Invoke();

            return true;
        }

        // ── Board helpers ───────────────────────────────────────────────────────────

        public int FindFirstSpaceIndexByType(BoardManager boardManager, BoardSpaceType type, int fallbackIndex)
        {
            if (boardManager == null)
            {
                return fallbackIndex;
            }

            for (int i = 0; i < boardManager.SpaceCount; i++)
            {
                BoardSpaceView space = boardManager.GetSpace(i);
                if (space != null && space.type == type)
                {
                    return space.index;
                }
            }

            return boardManager.NormalizeIndex(fallbackIndex);
        }

        // ── Economy messaging ───────────────────────────────────────────────────────

        public void SetEconomyMessage(string message)
        {
            _lastEconomyMessageNet.Value = new FixedString128Bytes(message ?? string.Empty);
        }

        public void SetLastResolvedSpaceIndex(int spaceIndex)
        {
            _lastResolvedSpaceIndexNet.Value = spaceIndex;
        }
    }
}
