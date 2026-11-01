using System.Collections.Generic;
using System.Linq;
using MonopolyGame.Board;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonopolyGame.Multiplayer.Editor
{
    /// <summary>
    /// One-time setup for the SCRUM-35 tile system. Builds the tile prefab,
    /// shared materials, template asset and board definition, then wires the
    /// Game scene to generate the board at runtime.
    /// </summary>
    public static class BoardTileAssetBuilder
    {
        private const string BoardFolder = "Assets/Board";
        private const string MaterialsFolder = BoardFolder + "/Materials";
        private const string ResourcesFolder = "Assets/Resources/Board";
        private const string PrefabPath = ResourcesFolder + "/BoardTile.prefab";
        private const string TemplatePath = BoardFolder + "/BoardTileTemplate.asset";
        private const string DefinitionPath = BoardFolder + "/BoardDefinition.asset";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        private sealed class TileMaterials
        {
            public Material Base;
            public Material Accent;
            public Material Icon;
            public Material Owner;
            public Material House;
        }

        [MenuItem("Monopoly Game/Board/Setup Tile System")]
        public static void SetupTileSystem()
        {
            EnsureFolders();

            TileMaterials materials = CreateMaterials();
            GameObject prefab = CreateOrLoadTilePrefab(materials);
            BoardTileTemplate template = CreateOrUpdateTemplate(prefab, materials);
            BoardDefinition definition = CreateOrUpdateDefinition(template);
            WireGameScene(definition);
            VerifyGeneratedBoard(definition);

            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BoardTileAssetBuilder] Tile system setup complete.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(BoardFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Board");
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder(BoardFolder, "Materials");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Board");
            }
        }

        private static TileMaterials CreateMaterials()
        {
            return new TileMaterials
            {
                Base = CreateOrLoadMaterial(MaterialsFolder + "/TileBase.mat"),
                Accent = CreateOrLoadMaterial(MaterialsFolder + "/TileAccent.mat"),
                Icon = CreateOrLoadMaterial(MaterialsFolder + "/TileIcon.mat"),
                Owner = CreateOrLoadMaterial(MaterialsFolder + "/TileOwner.mat"),
                House = CreateOrLoadMaterial(MaterialsFolder + "/TileHouse.mat")
            };
        }

        private static Material CreateOrLoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrLoadTilePrefab(TileMaterials materials)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("BoardTile");
            root.AddComponent<BoardSpaceView>();
            var tileView = root.AddComponent<BoardTileView>();
            var footprintCollider = root.AddComponent<BoxCollider>();
            footprintCollider.size = new Vector3(1f, 0.12f, 1f);

            var footprint = new GameObject("Footprint").transform;
            footprint.SetParent(root.transform, false);

            GameObject baseObject = CreatePrimitiveChild(
                "Base",
                PrimitiveType.Cube,
                footprint,
                Vector3.zero,
                Vector3.one,
                materials.Base);

            GameObject accent = CreatePrimitiveChild(
                "AccentBand",
                PrimitiveType.Cube,
                footprint,
                new Vector3(0f, 0.5f, 0.4f),
                new Vector3(0.9f, 0.5f, 0.16f),
                materials.Accent);

            GameObject icon = CreatePrimitiveChild(
                "Icon",
                PrimitiveType.Quad,
                footprint,
                new Vector3(0f, 0.51f, -0.2f),
                new Vector3(0.35f, 0.35f, 1f),
                materials.Icon);
            icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject ownerMarker = CreatePrimitiveChild(
                "OwnerMarker",
                PrimitiveType.Cube,
                footprint,
                new Vector3(0f, 0.5f, -0.45f),
                new Vector3(0.9f, 0.5f, 0.08f),
                materials.Owner);
            ownerMarker.SetActive(false);

            var housesRoot = new GameObject("Houses").transform;
            housesRoot.SetParent(root.transform, false);

            for (int i = 0; i < 4; i++)
            {
                GameObject house = CreatePrimitiveChild(
                    $"House_{i + 1}",
                    PrimitiveType.Cube,
                    housesRoot,
                    new Vector3(-0.3f + i * 0.2f, 0.09f, 0.55f),
                    new Vector3(0.12f, 0.12f, 0.12f),
                    materials.House);
                house.SetActive(false);
            }

            TMP_FontAsset font = ResolveFont();
            TextMeshPro nameLabel = CreateLabel("NameLabel", root.transform, new Vector3(0f, 0.08f, -0.35f), 0.35f, font);
            TextMeshPro priceLabel = CreateLabel("PriceLabel", root.transform, new Vector3(0f, 0.08f, 0.25f), 0.28f, font);

            var serializedTileView = new SerializedObject(tileView);
            serializedTileView.FindProperty("footprint").objectReferenceValue = footprint;
            serializedTileView.FindProperty("footprintCollider").objectReferenceValue = footprintCollider;
            serializedTileView.FindProperty("baseRenderer").objectReferenceValue = baseObject.GetComponent<MeshRenderer>();
            serializedTileView.FindProperty("accentRenderer").objectReferenceValue = accent.GetComponent<MeshRenderer>();
            serializedTileView.FindProperty("iconRenderer").objectReferenceValue = icon.GetComponent<MeshRenderer>();
            serializedTileView.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            serializedTileView.FindProperty("priceLabel").objectReferenceValue = priceLabel;
            serializedTileView.FindProperty("ownerMarkerRoot").objectReferenceValue = ownerMarker;
            serializedTileView.FindProperty("ownerMarkerRenderer").objectReferenceValue = ownerMarker.GetComponent<MeshRenderer>();
            serializedTileView.FindProperty("housesRoot").objectReferenceValue = housesRoot;
            serializedTileView.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static GameObject CreatePrimitiveChild(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;

            var renderer = child.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = child.GetComponent<Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return child;
        }

        private static TextMeshPro CreateLabel(
            string name,
            Transform parent,
            Vector3 localPosition,
            float fontSize,
            TMP_FontAsset font)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var label = labelObject.GetComponent<TextMeshPro>();
            label.text = name == "NameLabel" ? "Tile" : "100";
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(1.8f, 0.4f);

            if (font != null)
            {
                label.font = font;
            }

            return label;
        }

        private static BoardTileTemplate CreateOrUpdateTemplate(GameObject prefab, TileMaterials materials)
        {
            BoardTileTemplate template = AssetDatabase.LoadAssetAtPath<BoardTileTemplate>(TemplatePath);

            if (template == null)
            {
                template = ScriptableObject.CreateInstance<BoardTileTemplate>();
                AssetDatabase.CreateAsset(template, TemplatePath);
            }

            var serializedTemplate = new SerializedObject(template);
            serializedTemplate.FindProperty("tilePrefab").objectReferenceValue = prefab;
            serializedTemplate.FindProperty("baseMaterial").objectReferenceValue = materials.Base;
            serializedTemplate.FindProperty("accentMaterial").objectReferenceValue = materials.Accent;
            serializedTemplate.FindProperty("iconMaterial").objectReferenceValue = materials.Icon;
            serializedTemplate.FindProperty("ownerMarkerMaterial").objectReferenceValue = materials.Owner;
            serializedTemplate.FindProperty("font").objectReferenceValue = ResolveFont();

            SerializedProperty orientations = serializedTemplate.FindProperty("orientations");
            SerializedProperty cornerYaws = orientations != null ? orientations.FindPropertyRelative("cornerYaws") : null;

            if (cornerYaws != null)
            {
                cornerYaws.arraySize = 4;
                cornerYaws.GetArrayElementAtIndex(0).floatValue = 0f;
                cornerYaws.GetArrayElementAtIndex(1).floatValue = 90f;
                cornerYaws.GetArrayElementAtIndex(2).floatValue = 180f;
                cornerYaws.GetArrayElementAtIndex(3).floatValue = 270f;
            }

            serializedTemplate.ApplyModifiedPropertiesWithoutUndo();

            return template;
        }

        private static BoardDefinition CreateOrUpdateDefinition(BoardTileTemplate template)
        {
            BoardDefinition definition = AssetDatabase.LoadAssetAtPath<BoardDefinition>(DefinitionPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BoardDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("template").objectReferenceValue = template;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            if (definition.SpaceCount == 0)
            {
                List<BoardSpaceDefinition> spaces = ReadSpacesFromScene();

                if (spaces == null || spaces.Count == 0)
                {
                    spaces = BuildDefaultSpaces();
                }

                definition.SetSpaces(template, spaces);
            }

            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static List<BoardSpaceDefinition> ReadSpacesFromScene()
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
                    return null;
                }

                return boardManager
                    .GetComponentsInChildren<BoardSpaceView>(true)
                    .OrderBy(space => space.index)
                    .Select(space => new BoardSpaceDefinition(
                        space.index,
                        space.displayName,
                        space.type,
                        space.price,
                        space.baseRent,
                        space.houseCost,
                        GetColorGroup(space.index)))
                    .ToList();
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(gameScene, true);
                }
            }
        }

        private static void WireGameScene(BoardDefinition definition)
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
                    Debug.LogError("[BoardTileAssetBuilder] No BoardManager found in Game.unity.");
                    return;
                }

                GameObject boardObject = boardManager.gameObject;
                Transform spacesRoot = boardObject.transform.Find("Spaces");

                if (spacesRoot == null)
                {
                    var spacesObject = new GameObject("Spaces");
                    spacesObject.transform.SetParent(boardObject.transform, false);
                    spacesRoot = spacesObject.transform;
                }

                ClearChildren(spacesRoot);

                BoardGenerator generator = boardObject.GetComponent<BoardGenerator>();

                if (generator == null)
                {
                    generator = boardObject.AddComponent<BoardGenerator>();
                }

                var serializedGenerator = new SerializedObject(generator);
                serializedGenerator.FindProperty("definition").objectReferenceValue = definition;
                serializedGenerator.FindProperty("boardManager").objectReferenceValue = boardManager;
                serializedGenerator.FindProperty("spacesRoot").objectReferenceValue = spacesRoot;
                serializedGenerator.FindProperty("buildOnAwake").boolValue = true;
                serializedGenerator.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(gameScene);
                EditorSceneManager.SaveScene(gameScene);
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(gameScene, true);
                }
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void VerifyGeneratedBoard(BoardDefinition definition)
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
                BoardGenerator generator = gameScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BoardGenerator>(true))
                    .FirstOrDefault();

                if (generator == null)
                {
                    Debug.LogError("[BoardTileAssetBuilder] Verification failed: no BoardGenerator in Game.unity.");
                    return;
                }

                generator.BuildBoard(preview: true);

                BoardManager boardManager = generator.GetComponent<BoardManager>();
                Transform spacesRoot = boardManager != null ? boardManager.transform.Find("Spaces") : null;
                int generatedCount = spacesRoot != null
                    ? spacesRoot.GetComponentsInChildren<BoardSpaceView>(true).Length
                    : 0;

                if (generatedCount != definition.SpaceCount)
                {
                    Debug.LogError($"[BoardTileAssetBuilder] Verification failed: generated {generatedCount} tiles, expected {definition.SpaceCount}.");
                }
                else if (boardManager == null || boardManager.SpaceCount != definition.SpaceCount)
                {
                    Debug.LogError("[BoardTileAssetBuilder] Verification failed: BoardManager did not register all generated tiles.");
                }
                else
                {
                    BoardState state = boardManager.CaptureBoardState();
                    Debug.Log($"[BoardTileAssetBuilder] Verification passed: {state.SpaceCount} tiles, index 0 is {state.GetSpace(0)?.DisplayName}, index 39 is {state.GetSpace(39)?.DisplayName}.");
                }

                generator.ClearBoard();
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(gameScene, true);
                }
            }
        }

        private static TMP_FontAsset ResolveFont()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            if (font != null)
            {
                return font;
            }

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        private static List<BoardSpaceDefinition> BuildDefaultSpaces()
        {
            const int edgeSpacesPerSide = 9;
            int count = edgeSpacesPerSide * 4 + 4;
            var spaces = new List<BoardSpaceDefinition>(count);

            for (int i = 0; i < count; i++)
            {
                BoardSpaceType type = GetSpaceType(i);
                GetSpaceData(i, type, out int price, out int baseRent, out int houseCost);

                spaces.Add(new BoardSpaceDefinition(
                    i,
                    GetDefaultDisplayName(i, type),
                    type,
                    price,
                    baseRent,
                    houseCost,
                    GetColorGroup(i)));
            }

            return spaces;
        }

        private static BoardSpaceType GetSpaceType(int index)
        {
            return index switch
            {
                0 => BoardSpaceType.Start,
                10 => BoardSpaceType.Jail,
                20 => BoardSpaceType.Parking,
                30 => BoardSpaceType.GoToJail,
                4 or 38 => BoardSpaceType.Tax,
                5 or 7 or 22 or 25 or 35 or 36 => BoardSpaceType.ActionCard,
                2 or 12 or 15 or 17 or 28 or 33 => BoardSpaceType.EventCard,
                _ => BoardSpaceType.Property
            };
        }

        private static string GetDefaultDisplayName(int index, BoardSpaceType type)
        {
            return index switch
            {
                0 => "Start",
                10 => "Jail",
                20 => "Parking",
                30 => "Go To Jail",
                _ => type.ToString()
            };
        }

        private static void GetSpaceData(int index, BoardSpaceType type, out int price, out int baseRent, out int houseCost)
        {
            price = 0;
            baseRent = 0;
            houseCost = 0;

            if (type == BoardSpaceType.Tax)
            {
                price = index == 4 ? 200 : 100;
                return;
            }

            if (type != BoardSpaceType.Property)
            {
                return;
            }

            switch (index)
            {
                case 1:
                case 3:
                    price = 60;
                    baseRent = index == 1 ? 2 : 4;
                    houseCost = 50;
                    return;
                case 6:
                case 8:
                    price = 100;
                    baseRent = 6;
                    houseCost = 50;
                    return;
                case 9:
                    price = 120;
                    baseRent = 8;
                    houseCost = 50;
                    return;
                case 11:
                case 13:
                    price = 140;
                    baseRent = 10;
                    houseCost = 100;
                    return;
                case 14:
                    price = 160;
                    baseRent = 12;
                    houseCost = 100;
                    return;
                case 16:
                case 18:
                case 19:
                    price = 200;
                    baseRent = 25;
                    houseCost = 0;
                    return;
                default:
                    price = 200;
                    baseRent = 15;
                    houseCost = 100;
                    return;
            }
        }

        private static int GetColorGroup(int index)
        {
            return index switch
            {
                1 or 3 => 1,
                6 or 8 or 9 => 2,
                11 or 13 or 14 => 3,
                16 or 18 or 19 => 4,
                21 or 23 or 24 => 5,
                26 or 27 or 29 => 6,
                31 or 32 or 34 => 7,
                37 or 39 => 8,
                _ => 0
            };
        }
    }
}
