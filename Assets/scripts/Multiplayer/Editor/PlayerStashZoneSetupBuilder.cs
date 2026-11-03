using System.Linq;
using MonopolyGame.Board;
using MonopolyGame.Stash;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonopolyGame.Multiplayer.Editor
{
    /// <summary>
    /// One-time wiring for the SCRUM-39 stash zones: attaches the runtime system
    /// to the Board, removes the old one-side PlayerZone placeholder and widens
    /// the board base into a table surface that carries the four outer zones.
    /// </summary>
    public static class PlayerStashZoneSetupBuilder
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const float TableSize = 24.5f;

        [MenuItem("Monopoly Game/Board/Setup Player Stash Zones")]
        public static void SetupStashZones()
        {
            Scene gameScene = SceneManager.GetSceneByPath(GameScenePath);
            bool openedAdditively = false;

            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            try
            {
                BoardManager boardManager = gameScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BoardManager>(true))
                    .FirstOrDefault();

                if (boardManager == null)
                {
                    Debug.LogError("[PlayerStashZoneSetupBuilder] No BoardManager found in Game.unity.");
                    return;
                }

                GameObject boardObject = boardManager.gameObject;
                BoardGenerator generator = boardObject.GetComponent<BoardGenerator>();
                PlayerStashZoneSystem system = boardObject.GetComponent<PlayerStashZoneSystem>();

                if (system == null)
                {
                    system = boardObject.AddComponent<PlayerStashZoneSystem>();
                }

                var serializedSystem = new SerializedObject(system);

                if (generator != null)
                {
                    serializedSystem.FindProperty("boardGenerator").objectReferenceValue = generator;
                }

                serializedSystem.ApplyModifiedPropertiesWithoutUndo();

                Transform decor = boardObject.transform.Find("Decor");
                Transform legacyZone = decor != null ? decor.Find("PlayerZone") : null;

                if (legacyZone != null)
                {
                    Object.DestroyImmediate(legacyZone.gameObject);
                }

                Transform boardBase = decor != null ? decor.Find("BoardBase") : null;

                if (boardBase != null)
                {
                    Vector3 scale = boardBase.localScale;
                    boardBase.localScale = new Vector3(TableSize, scale.y, TableSize);
                }
                else
                {
                    Debug.LogWarning("[PlayerStashZoneSetupBuilder] Decor/BoardBase was not found; outer zones may float.");
                }

                bool layoutValid = system.ValidateLayout();
                ValidateTableBounds(generator, layoutValid);

                EditorSceneManager.MarkSceneDirty(gameScene);
                EditorSceneManager.SaveScene(gameScene);

                Debug.Log("[PlayerStashZoneSetupBuilder] Player stash zones setup complete.");
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(gameScene, true);
                }
            }
        }

        private static void ValidateTableBounds(BoardGenerator generator, bool layoutValid)
        {
            if (!layoutValid)
            {
                Debug.LogError("[PlayerStashZoneSetupBuilder] Stash layout validation failed.");
                return;
            }

            var config = new PlayerStashVisualConfig();
            BoardTileTemplate template = generator != null && generator.Definition != null
                ? generator.Definition.Template
                : null;
            PlayerStashMetrics metrics = PlayerStashLayout.FromTemplate(
                template,
                config.zoneGap,
                config.zoneDepth,
                config.zoneWidthMargin);

            float zoneOuterEdge = metrics.ZoneCenterDistance + metrics.ZoneDepth * 0.5f;
            float tableHalfSize = TableSize * 0.5f;

            if (zoneOuterEdge > tableHalfSize)
            {
                Debug.LogError(
                    $"[PlayerStashZoneSetupBuilder] Zone outer edge {zoneOuterEdge:0.00} exceeds table half-size {tableHalfSize:0.00}.");
            }
            else
            {
                Debug.Log(
                    $"[PlayerStashZoneSetupBuilder] Table bounds valid: zone edge {zoneOuterEdge:0.00} < table half {tableHalfSize:0.00}.");
            }
        }
    }
}
