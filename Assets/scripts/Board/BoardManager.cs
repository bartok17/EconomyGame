using System;
using System.Collections.Generic;
using System.Linq;
using MonopolyGame.Multiplayer.Gameplay;
using UnityEngine;

namespace MonopolyGame.Board
{
    public sealed class BoardManager : MonoBehaviour
    {
        [SerializeField] private Transform spacesRoot;

        private readonly List<BoardSpaceView> spaces = new List<BoardSpaceView>();

        public int SpaceCount => spaces.Count;

        private void Awake()
        {
            RefreshSpaces();
        }

        [ContextMenu("Refresh Spaces")]
        public void RefreshSpaces()
        {
            if (spacesRoot == null)
            {
                spacesRoot = transform.Find("Spaces");
            }

            spaces.Clear();

            Transform searchRoot = spacesRoot != null ? spacesRoot : transform;
            BoardSpaceView[] foundSpaces = searchRoot.GetComponentsInChildren<BoardSpaceView>(true);

            Array.Sort(foundSpaces, (left, right) => left.index.CompareTo(right.index));
            spaces.AddRange(foundSpaces);

            Debug.Log($"[BoardManager] Loaded {spaces.Count} board spaces.");
        }

        public BoardSpaceView GetSpace(int index)
        {
            if (spaces.Count == 0)
            {
                RefreshSpaces();
            }

            int normalizedIndex = NormalizeIndex(index);

            for (int i = 0; i < spaces.Count; i++)
            {
                if (spaces[i].index == normalizedIndex)
                {
                    return spaces[i];
                }
            }

            throw new InvalidOperationException($"Board space with index {normalizedIndex} was not found.");
        }

        public Vector3 GetPawnWorldPosition(int spaceIndex, int pawnSlot)
        {
            return GetSpace(spaceIndex).GetPawnWorldPosition(pawnSlot);
        }

        public BoardState CaptureBoardState()
        {
            if (spaces.Count == 0)
            {
                RefreshSpaces();
            }

            return new BoardState(spaces.Where(space => space != null).Select(space => new BoardSpaceSnapshot(
                space.index,
                space.displayName,
                space.type,
                space.price,
                space.baseRent,
                space.houseCost,
                space.ownerId,
                space.housesBuilt)));
        }

        public int NormalizeIndex(int index)
        {
            if (spaces.Count == 0)
            {
                RefreshSpaces();
            }

            if (spaces.Count == 0)
            {
                return 0;
            }

            int normalized = index % spaces.Count;
            return normalized < 0 ? normalized + spaces.Count : normalized;
        }

        private void OnDrawGizmosSelected()
        {
            RefreshSpaces();

            Gizmos.color = Color.yellow;

            for (int i = 0; i < spaces.Count; i++)
            {
                if (spaces[i] == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(spaces[i].GetPawnWorldPosition(0), 0.12f);
            }
        }
    }
}