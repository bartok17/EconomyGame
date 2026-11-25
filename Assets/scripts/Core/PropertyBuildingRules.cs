using System;
using System.Collections.Generic;

namespace MonopolyGame.Board
{
    public readonly struct PropertyBuildCheck
    {
        public PropertyBuildCheck(bool allowed, string reason, int nextLevel, int amount)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
            NextLevel = nextLevel;
            Amount = amount;
        }

        public bool Allowed { get; }

        public string Reason { get; }

        public int NextLevel { get; }

        public int Amount { get; }

        public static PropertyBuildCheck Denied(string reason)
        {
            return new PropertyBuildCheck(false, reason, 0, 0);
        }

        public static PropertyBuildCheck Permitted(int nextLevel, int amount)
        {
            return new PropertyBuildCheck(true, string.Empty, nextLevel, amount);
        }
    }

    public static class PropertyBuildingRules
    {
        public const int MaxHouses = 4;
        public const int HotelLevel = MaxHouses + 1;
        public const int DefaultHousesPerLap = 4;

        private static readonly int[] DefaultRentMultipliers = { 1, 2, 3, 4, 5, 7 };

        public static bool IsHotel(int level)
        {
            return level >= HotelLevel;
        }

        public static int ClampLevel(int level)
        {
            if (level < 0)
            {
                return 0;
            }

            return level > HotelLevel ? HotelLevel : level;
        }

        public static int[] SanitizeRentMultipliers(int[] values)
        {
            var sanitized = new int[HotelLevel + 1];

            for (int i = 0; i < sanitized.Length; i++)
            {
                int value = values != null && i < values.Length ? values[i] : DefaultRentMultipliers[i];
                sanitized[i] = value > 0 ? value : DefaultRentMultipliers[i];
            }

            return sanitized;
        }

        public static int CalculateRent(int baseRent, int level, IReadOnlyList<int> rentMultipliers)
        {
            if (baseRent <= 0)
            {
                return 0;
            }

            IReadOnlyList<int> table = rentMultipliers != null && rentMultipliers.Count > 0
                ? rentMultipliers
                : DefaultRentMultipliers;

            int index = ClampLevel(level);

            if (index >= table.Count)
            {
                index = table.Count - 1;
            }

            int multiplier = table[index];
            return baseRent * (multiplier > 0 ? multiplier : 1);
        }

        public static int SellRefund(int houseCost)
        {
            return houseCost > 0 ? houseCost / 2 : 0;
        }

        public static PropertyBuildCheck CheckBuild(
            int level,
            int lowestLevelInGroup,
            bool ownsWholeGroup,
            int houseCost,
            int balance,
            int housesBuiltThisLap,
            int housesPerLap)
        {
            if (!ownsWholeGroup)
            {
                return PropertyBuildCheck.Denied("You need the whole color set to build.");
            }

            if (houseCost <= 0)
            {
                return PropertyBuildCheck.Denied("This property cannot be built on.");
            }

            if (IsHotel(level))
            {
                return PropertyBuildCheck.Denied("This property already has a hotel.");
            }

            // A hotel is a build too, so it consumes the same per-lap allowance.
            if (housesPerLap > 0 && housesBuiltThisLap >= housesPerLap)
            {
                return PropertyBuildCheck.Denied($"Building limit reached ({housesBuiltThisLap}/{housesPerLap}) — pass Start to build again.");
            }

            if (level > lowestLevelInGroup)
            {
                return PropertyBuildCheck.Denied("Build evenly across the color set.");
            }

            if (balance < houseCost)
            {
                return PropertyBuildCheck.Denied($"Not enough money to build (needs {houseCost}).");
            }

            return PropertyBuildCheck.Permitted(level + 1, houseCost);
        }

        public static PropertyBuildCheck CheckSell(int level, int highestLevelInGroup, int houseCost)
        {
            if (level <= 0)
            {
                return PropertyBuildCheck.Denied("There are no houses to sell.");
            }

            if (level < highestLevelInGroup)
            {
                return PropertyBuildCheck.Denied("Sell evenly across the color set.");
            }

            return PropertyBuildCheck.Permitted(level - 1, SellRefund(houseCost));
        }
    }
}
