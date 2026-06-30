using System.Collections.Generic;

namespace MonopolyGame.Board
{
    public sealed class BoardSpaceSnapshot
    {
        public BoardSpaceSnapshot(
            int index,
            string displayName,
            BoardSpaceType spaceType,
            int price,
            int baseRent,
            int houseCost,
            string ownerId,
            int housesBuilt)
        {
            Index = index;
            DisplayName = displayName ?? string.Empty;
            SpaceType = spaceType;
            Price = price;
            BaseRent = baseRent;
            HouseCost = houseCost;
            OwnerId = ownerId ?? string.Empty;
            HousesBuilt = housesBuilt;
        }

        public int Index { get; }
        public string DisplayName { get; }
        public BoardSpaceType SpaceType { get; }
        public int Price { get; }
        public int BaseRent { get; }
        public int HouseCost { get; }
        public string OwnerId { get; }
        public int HousesBuilt { get; }
    }

    public sealed class BoardState
    {
        private readonly List<BoardSpaceSnapshot> spaces = new List<BoardSpaceSnapshot>();

        public BoardState(IEnumerable<BoardSpaceSnapshot> boardSpaces)
        {
            if (boardSpaces != null)
            {
                spaces.AddRange(boardSpaces);
            }
        }

        public int SpaceCount => spaces.Count;

        public BoardSpaceSnapshot GetSpace(int index)
        {
            if (spaces.Count == 0)
            {
                return null;
            }

            int normalized = NormalizeIndex(index);

            for (int i = 0; i < spaces.Count; i++)
            {
                BoardSpaceSnapshot snapshot = spaces[i];
                if (snapshot != null && snapshot.Index == normalized)
                {
                    return snapshot;
                }
            }

            return null;
        }

        public int NormalizeIndex(int index)
        {
            if (spaces.Count == 0)
            {
                return 0;
            }

            int normalized = index % spaces.Count;
            return normalized < 0 ? normalized + spaces.Count : normalized;
        }
    }
}
