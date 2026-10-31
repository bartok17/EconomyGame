using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// The full board definition. Entries are ordered by <see cref="BoardSpaceDefinition.Index"/>
    /// and are used by <see cref="BoardGenerator"/> to build the board at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Monopoly Game/Board/Board Definition", fileName = "BoardDefinition")]
    public sealed class BoardDefinition : ScriptableObject
    {
        [SerializeField] private BoardTileTemplate template;
        [SerializeField] private List<BoardSpaceDefinition> spaces = new List<BoardSpaceDefinition>();

        public BoardTileTemplate Template => template;
        public IReadOnlyList<BoardSpaceDefinition> Spaces => spaces;
        public int SpaceCount => spaces != null ? spaces.Count : 0;

        public BoardSpaceDefinition Get(int index)
        {
            if (spaces == null || spaces.Count == 0)
            {
                return null;
            }

            if (index >= 0 && index < spaces.Count && spaces[index] != null && spaces[index].Index == index)
            {
                return spaces[index];
            }

            for (int i = 0; i < spaces.Count; i++)
            {
                if (spaces[i] != null && spaces[i].Index == index)
                {
                    return spaces[i];
                }
            }

            return null;
        }

        public bool Validate(out string error)
        {
            if (template == null)
            {
                error = "No BoardTileTemplate assigned.";
                return false;
            }

            if (spaces == null || spaces.Count == 0)
            {
                error = "Board has no spaces.";
                return false;
            }

            bool hasStart = false;

            for (int i = 0; i < spaces.Count; i++)
            {
                BoardSpaceDefinition space = spaces[i];

                if (space == null)
                {
                    error = $"Space at position {i} is null.";
                    return false;
                }

                if (space.Index != i)
                {
                    error = $"Space at position {i} has index {space.Index}. Indices must be contiguous and ordered.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(space.DisplayName))
                {
                    error = $"Space {i} has no display name.";
                    return false;
                }

                if (space.Price < 0 || space.BaseRent < 0 || space.HouseCost < 0)
                {
                    error = $"Space {i} has a negative price, rent or house cost.";
                    return false;
                }

                if (space.Type == BoardSpaceType.Start)
                {
                    if (hasStart)
                    {
                        error = "Board contains more than one Start space.";
                        return false;
                    }

                    hasStart = true;
                }
            }

            if (!hasStart)
            {
                error = "Board must contain exactly one Start space.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>Used by the editor migration tool and tests.</summary>
        public void SetSpaces(BoardTileTemplate tileTemplate, IEnumerable<BoardSpaceDefinition> definitions)
        {
            template = tileTemplate;
            spaces.Clear();

            if (definitions == null)
            {
                return;
            }

            spaces.AddRange(definitions);
        }
    }
}
