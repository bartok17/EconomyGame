using TMPro;
using UnityEngine;

namespace MonopolyGame.Stash
{
    /// <summary>
    /// Presentation for one marked player stash zone. Owns the plate, the
    /// player-colour accent, the name label and the six denomination piles.
    /// Public metadata is intentionally exposed for the future hover info card.
    /// </summary>
    public sealed class PlayerStashZoneView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly MoneyPileView[] piles = new MoneyPileView[MoneyNotes.Count];
        private readonly int[] noteCounts = new int[MoneyNotes.Count];

        private PlayerStashVisualConfig config;
        private Renderer baseRenderer;
        private Renderer accentRenderer;
        private TMP_Text nameLabel;
        private MaterialPropertyBlock baseBlock;
        private MaterialPropertyBlock accentBlock;

        private int pawnSlot;
        private string displayName = string.Empty;
        private int balance;
        private bool isOccupied;

        public int PawnSlot => pawnSlot;
        public string DisplayName => displayName;
        public int Balance => balance;
        public bool IsOccupied => isOccupied;
        public int PileCount => piles.Length;

        /// <summary>World-space point the camera should frame for this zone.</summary>
        public Vector3 ZoneCenter => transform.position + Vector3.up * 0.15f;

        public MoneyPileView GetPile(int index)
        {
            return index >= 0 && index < piles.Length ? piles[index] : null;
        }

        public void Setup(
            int pawnSlot,
            PlayerStashMetrics metrics,
            PlayerStashVisualConfig visualConfig,
            TMP_FontAsset font,
            Material surfaceMaterial)
        {
            this.pawnSlot = pawnSlot;
            config = visualConfig ?? new PlayerStashVisualConfig();

            transform.localRotation = Quaternion.Euler(0f, PlayerStashLayout.GetZoneYaw(pawnSlot), 0f);

            baseBlock = new MaterialPropertyBlock();
            accentBlock = new MaterialPropertyBlock();

            BuildPlate(metrics, surfaceMaterial);
            BuildPiles(metrics, surfaceMaterial);
            BuildLabel(metrics, font);

            Apply(0, string.Empty, Color.white, false);
        }

        public void Apply(int newBalance, string playerName, Color playerColor, bool occupied)
        {
            balance = Mathf.Max(0, newBalance);
            displayName = playerName ?? string.Empty;
            isOccupied = occupied;

            Color plateColor = isOccupied
                ? Color.Lerp(config.neutralBase, playerColor, config.occupiedTint)
                : config.neutralBase;
            Color accentColor = isOccupied ? playerColor : config.emptyAccent;

            SetRendererColor(baseRenderer, baseBlock, plateColor);
            SetRendererColor(accentRenderer, accentBlock, accentColor);

            if (nameLabel != null)
            {
                bool showLabel = isOccupied && !string.IsNullOrEmpty(displayName);
                nameLabel.gameObject.SetActive(showLabel);

                if (showLabel)
                {
                    nameLabel.text = displayName;
                    nameLabel.color = Color.Lerp(playerColor, Color.white, 0.4f);
                    nameLabel.ForceMeshUpdate();
                }
            }

            for (int i = 0; i < piles.Length; i++)
            {
                if (piles[i] != null)
                {
                    piles[i].gameObject.SetActive(isOccupied);
                }
            }

            if (!isOccupied)
            {
                for (int i = 0; i < piles.Length; i++)
                {
                    piles[i]?.SetNoteCount(0);
                }

                return;
            }

            MoneyNotes.Decompose(balance, noteCounts);

            for (int i = 0; i < piles.Length; i++)
            {
                piles[i]?.SetNoteCount(noteCounts[i]);
            }
        }

        private void BuildPlate(PlayerStashMetrics metrics, Material surfaceMaterial)
        {
            baseRenderer = CreateBox(
                "Plate",
                transform,
                Vector3.zero,
                new Vector3(metrics.ZoneWidth, config.zonePlateHeight, metrics.ZoneDepth),
                surfaceMaterial,
                keepCollider: true);

            accentRenderer = CreateBox(
                "Accent",
                transform,
                new Vector3(0f, config.zonePlateHeight * 0.5f + 0.01f, -metrics.ZoneDepth * 0.5f + 0.08f),
                new Vector3(metrics.ZoneWidth - 0.12f, 0.02f, 0.12f),
                surfaceMaterial,
                keepCollider: false);
        }

        private void BuildPiles(PlayerStashMetrics metrics, Material surfaceMaterial)
        {
            float spacing = metrics.GetPileSpacing(
                config.pileColumnSpacing,
                config.pileColumnMargin,
                MoneyNotes.Count);
            float columnX = metrics.GetPileColumnX(
                config.banknoteWidth,
                config.pileColumnRightMargin);
            float rootY = config.zonePlateHeight * 0.5f + 0.005f;

            for (int i = 0; i < MoneyNotes.Count; i++)
            {
                var pileObject = new GameObject($"Money_{MoneyNotes.Denominations[i]}");
                pileObject.transform.SetParent(transform, false);
                pileObject.transform.localPosition = new Vector3(
                    columnX,
                    rootY,
                    metrics.GetPileLocalZ(i, MoneyNotes.Count, spacing));

                MoneyPileView pile = pileObject.AddComponent<MoneyPileView>();
                pile.Setup(
                    pawnSlot,
                    MoneyNotes.Denominations[i],
                    config.GetNoteColor(i),
                    config,
                    surfaceMaterial,
                    config.GetNoteMaterial(i));

                piles[i] = pile;
            }
        }

        private void BuildLabel(PlayerStashMetrics metrics, TMP_FontAsset font)
        {
            var labelObject = new GameObject("NameLabel", typeof(RectTransform), typeof(TextMeshPro));
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(
                -2.7f,
                config.zonePlateHeight * 0.5f + 0.035f,
                0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            nameLabel = labelObject.GetComponent<TextMeshPro>();

            if (font != null)
            {
                nameLabel.font = font;
            }

            nameLabel.text = string.Empty;
            nameLabel.fontSize = 7f;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 2f;
            nameLabel.fontSizeMax = 7f;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.raycastTarget = false;
            nameLabel.rectTransform.sizeDelta = new Vector2(
                Mathf.Min(8.2f, metrics.ZoneWidth - 1f),
                1.2f);
            nameLabel.ForceMeshUpdate();
        }

        private static Renderer CreateBox(
            string boxName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;

            Renderer renderer = box.GetComponent<Renderer>();

            if (material != null && renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = box.GetComponent<Collider>();

                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(collider);
                    }
                    else
                    {
                        Object.DestroyImmediate(collider);
                    }
                }
            }

            return renderer;
        }

        private static void SetRendererColor(
            Renderer renderer,
            MaterialPropertyBlock block,
            Color color)
        {
            if (renderer == null || block == null)
            {
                return;
            }

            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
