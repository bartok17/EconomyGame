using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Builds the board at runtime from <see cref="BoardDefinition"/> and
    /// <see cref="BoardTileTemplate"/>. Runs before <see cref="BoardManager"/>
    /// so tile lookup, camera focus and session binding see a complete board.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BoardGenerator : MonoBehaviour
    {
        [SerializeField] private BoardDefinition definition;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private Transform spacesRoot;
        [SerializeField] private bool buildOnAwake = true;

        public BoardDefinition Definition => definition;
        public bool IsBuilt { get; private set; }

        private void Awake()
        {
            if (buildOnAwake)
            {
                BuildBoard();
            }
        }

        [ContextMenu("Build Preview")]
        private void BuildPreview()
        {
            BuildBoard(preview: true);
        }

        [ContextMenu("Clear Preview")]
        private void ClearPreview()
        {
            ClearBoard();
        }

        public void ClearBoard()
        {
            ResolveReferences();
            ClearSpaces();

            if (boardManager != null)
            {
                boardManager.RefreshSpaces();
            }

            IsBuilt = false;
        }

        [ContextMenu("Validate Definition")]
        private void ValidateDefinition()
        {
            if (definition == null)
            {
                Debug.LogError($"[{nameof(BoardGenerator)}] No BoardDefinition assigned.");
                return;
            }

            if (definition.Validate(out string error))
            {
                Debug.Log($"[{nameof(BoardGenerator)}] Definition is valid: {definition.SpaceCount} spaces.");
            }
            else
            {
                Debug.LogError($"[{nameof(BoardGenerator)}] Definition is invalid: {error}");
            }
        }

        public void BuildBoard(bool preview = false)
        {
            if (definition == null)
            {
                Debug.LogError($"[{nameof(BoardGenerator)}] Cannot build the board without a BoardDefinition.");
                return;
            }

            if (!definition.Validate(out string error))
            {
                Debug.LogError($"[{nameof(BoardGenerator)}] Cannot build the board: {error}");
                return;
            }

            BoardTileTemplate template = definition.Template;

            if (template == null)
            {
                Debug.LogError($"[{nameof(BoardGenerator)}] Cannot build the board without a BoardTileTemplate.");
                return;
            }

            ResolveReferences();
            ClearSpaces();

            int builtCount = 0;

            for (int i = 0; i < definition.SpaceCount; i++)
            {
                BoardSpaceDefinition space = definition.Get(i);

                if (space == null)
                {
                    Debug.LogWarning($"[{nameof(BoardGenerator)}] Space {i} is missing from the definition.");
                    continue;
                }

                BoardTilePlacement placement = BoardLayoutCalculator.GetPlacement(space.Index, template.EdgeSpacesPerSide, template);
                GameObject prefab = space.PrefabOverride != null ? space.PrefabOverride : template.ResolvePrefab(placement.IsCorner);

                if (prefab == null)
                {
                    Debug.LogError($"[{nameof(BoardGenerator)}] No prefab configured for space {space.Index}.");
                    continue;
                }

                GameObject instance = Instantiate(prefab, spacesRoot);
                instance.name = $"Space_{space.Index:00}_{SanitizeName(space.DisplayName)}";
                instance.transform.localPosition = placement.Position;
                instance.transform.localRotation = Quaternion.Euler(0f, placement.YRotation, 0f);

                if (preview)
                {
                    instance.hideFlags = HideFlags.DontSave;
                }

                BoardSpaceView spaceView = instance.GetComponent<BoardSpaceView>();

                if (spaceView == null)
                {
                    spaceView = instance.AddComponent<BoardSpaceView>();
                }

                spaceView.Configure(space, template, placement.IsCorner);

                BoardTileView tileView = instance.GetComponent<BoardTileView>();

                if (tileView != null)
                {
                    tileView.Apply(space, template, placement);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(BoardGenerator)}] Tile prefab for space {space.Index} has no {nameof(BoardTileView)}.");
                }

                if (instance.GetComponentInChildren<Collider>(true) == null)
                {
                    Debug.LogWarning($"[{nameof(BoardGenerator)}] Tile prefab for space {space.Index} has no collider; camera focus will not work.");
                }

                builtCount++;
            }

            if (boardManager == null)
            {
                boardManager = GetComponent<BoardManager>();
            }

            if (boardManager != null)
            {
                boardManager.RefreshSpaces();
            }

            IsBuilt = true;
            Debug.Log($"[{nameof(BoardGenerator)}] Generated {builtCount} board tiles{(preview ? " (preview)" : string.Empty)}.");
        }

        private void ResolveReferences()
        {
            if (boardManager == null)
            {
                boardManager = GetComponent<BoardManager>();
            }

            if (spacesRoot != null)
            {
                return;
            }

            spacesRoot = transform.Find("Spaces");

            if (spacesRoot == null)
            {
                var spacesObject = new GameObject("Spaces");
                spacesObject.transform.SetParent(transform, false);
                spacesRoot = spacesObject.transform;
            }
        }

        private void ClearSpaces()
        {
            if (spacesRoot == null)
            {
                return;
            }

            for (int i = spacesRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = spacesRoot.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Space";
            }

            return value.Trim().Replace(' ', '_');
        }
    }
}
