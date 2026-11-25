using System;
using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Pawns;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class PropertyBuildingService
    {
        private readonly IPlayerEconomyService _economy;
        private readonly BoardManager _board;
        private readonly int _housesPerLap;
        private readonly int[] _rentMultipliers;

        public PropertyBuildingService(
            IPlayerEconomyService economy,
            BoardManager board,
            int housesPerLap,
            int[] rentMultipliers)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _board = board;
            _housesPerLap = housesPerLap < 0 ? 0 : housesPerLap;
            _rentMultipliers = PropertyBuildingRules.SanitizeRentMultipliers(rentMultipliers);
        }

        public int HousesPerLap => _housesPerLap;

        public int GetRent(int spaceIndex)
        {
            if (!TryGetProperty(spaceIndex, out BoardSpaceView space))
            {
                return 0;
            }

            return PropertyBuildingRules.CalculateRent(space.baseRent, _economy.GetPropertyHouses(spaceIndex), _rentMultipliers);
        }

        public PropertyBuildCheck EvaluateBuild(int spaceIndex, int pawnSlot)
        {
            if (!TryGetProperty(spaceIndex, out BoardSpaceView space))
            {
                return PropertyBuildCheck.Denied("This tile is not a property.");
            }

            int ownerSlot = _economy.GetPropertyOwnerPawnSlot(spaceIndex);

            if (ownerSlot < 0)
            {
                return PropertyBuildCheck.Denied("Buy this property first.");
            }

            if (ownerSlot != pawnSlot)
            {
                return PropertyBuildCheck.Denied("Another player owns this property.");
            }

            IReadOnlyList<BoardSpaceView> group = _board.GetPropertiesInGroup(space.colorGroup);

            return PropertyBuildingRules.CheckBuild(
                _economy.GetPropertyHouses(spaceIndex),
                GetLowestLevel(group),
                group.Count > 0 && AllOwnedBy(group, pawnSlot),
                space.houseCost,
                _economy.GetBalance(pawnSlot),
                _economy.GetHousesBuiltThisLap(pawnSlot),
                _housesPerLap);
        }

        public PropertyBuildCheck EvaluateSell(int spaceIndex, int pawnSlot)
        {
            if (!TryGetProperty(spaceIndex, out BoardSpaceView space))
            {
                return PropertyBuildCheck.Denied("This tile is not a property.");
            }

            int ownerSlot = _economy.GetPropertyOwnerPawnSlot(spaceIndex);

            if (ownerSlot < 0)
            {
                return PropertyBuildCheck.Denied("This property has no owner.");
            }

            if (ownerSlot != pawnSlot)
            {
                return PropertyBuildCheck.Denied("Another player owns this property.");
            }

            IReadOnlyList<BoardSpaceView> group = _board.GetPropertiesInGroup(space.colorGroup);

            return PropertyBuildingRules.CheckSell(
                _economy.GetPropertyHouses(spaceIndex),
                GetHighestLevel(group),
                space.houseCost);
        }

        public bool TryBuild(int spaceIndex, PlayerPawnNetworkSync pawn, out string message)
        {
            message = string.Empty;

            if (pawn == null || !TryGetProperty(spaceIndex, out BoardSpaceView space))
            {
                message = "The property could not be resolved.";
                return false;
            }

            PropertyBuildCheck check = EvaluateBuild(spaceIndex, pawn.PawnSlot);

            if (!check.Allowed)
            {
                message = check.Reason;
                return false;
            }

            if (!_economy.DeductBalance(pawn.PawnSlot, check.Amount))
            {
                message = "Not enough money to build.";
                return false;
            }

            _economy.SetPropertyHouses(spaceIndex, check.NextLevel);
            _economy.SetHousesBuiltThisLap(pawn.PawnSlot, _economy.GetHousesBuiltThisLap(pawn.PawnSlot) + 1);

            message = PropertyBuildingRules.IsHotel(check.NextLevel)
                ? $"{pawn.DisplayName} upgraded {space.displayName} to a hotel for {check.Amount}."
                : $"{pawn.DisplayName} built a house on {space.displayName} for {check.Amount}.";

            return true;
        }

        public bool TrySell(int spaceIndex, PlayerPawnNetworkSync pawn, out string message)
        {
            message = string.Empty;

            if (pawn == null || !TryGetProperty(spaceIndex, out BoardSpaceView space))
            {
                message = "The property could not be resolved.";
                return false;
            }

            int levelBefore = _economy.GetPropertyHouses(spaceIndex);
            PropertyBuildCheck check = EvaluateSell(spaceIndex, pawn.PawnSlot);

            if (!check.Allowed)
            {
                message = check.Reason;
                return false;
            }

            _economy.AddBalance(pawn.PawnSlot, check.Amount);
            _economy.SetPropertyHouses(spaceIndex, check.NextLevel);

            message = PropertyBuildingRules.IsHotel(levelBefore)
                ? $"{pawn.DisplayName} sold the hotel on {space.displayName} for {check.Amount} (4 houses remain)."
                : $"{pawn.DisplayName} sold a house on {space.displayName} for {check.Amount}.";

            return true;
        }

        private bool TryGetProperty(int spaceIndex, out BoardSpaceView space)
        {
            space = null;

            if (_board == null || _board.SpaceCount <= 0 || spaceIndex < 0 || spaceIndex >= _board.SpaceCount)
            {
                return false;
            }

            space = _board.GetSpace(spaceIndex);
            return space != null && space.type == BoardSpaceType.Property;
        }

        private bool AllOwnedBy(IReadOnlyList<BoardSpaceView> group, int pawnSlot)
        {
            for (int i = 0; i < group.Count; i++)
            {
                if (_economy.GetPropertyOwnerPawnSlot(group[i].index) != pawnSlot)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetLowestLevel(IReadOnlyList<BoardSpaceView> group)
        {
            if (group.Count == 0)
            {
                return 0;
            }

            int lowest = PropertyBuildingRules.HotelLevel;

            for (int i = 0; i < group.Count; i++)
            {
                int level = _economy.GetPropertyHouses(group[i].index);

                if (level < lowest)
                {
                    lowest = level;
                }
            }

            return lowest;
        }

        private int GetHighestLevel(IReadOnlyList<BoardSpaceView> group)
        {
            int highest = 0;

            for (int i = 0; i < group.Count; i++)
            {
                int level = _economy.GetPropertyHouses(group[i].index);

                if (level > highest)
                {
                    highest = level;
                }
            }

            return highest;
        }
    }
}
