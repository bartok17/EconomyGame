using TMPro;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Presentation for one generated tile. Holds references to the template
    /// prefab's children and applies per-tile data without touching gameplay code.
    /// </summary>
    public sealed class BoardTileView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Footprint")]
        [SerializeField] private Transform footprint;
        [SerializeField] private BoxCollider footprintCollider;
        [SerializeField] private Renderer baseRenderer;
        [SerializeField] private Renderer accentRenderer;
        [SerializeField] private Renderer iconRenderer;

        [Header("Labels")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text priceLabel;

        [Header("State")]
        [SerializeField] private GameObject ownerMarkerRoot;
        [SerializeField] private Renderer ownerMarkerRenderer;
        [SerializeField] private Transform housesRoot;
        [SerializeField] private GameObject highlightRoot;

        private MaterialPropertyBlock propertyBlock;
        private BoardTileTemplate template;
        private BoardSpaceDefinition definition;
        private TileSide side;

        public BoardSpaceDefinition Definition => definition;
        public TileSide Side => side;

        public void Apply(BoardSpaceDefinition spaceDefinition, BoardTileTemplate tileTemplate, BoardTilePlacement placement)
        {
            template = tileTemplate;
            definition = spaceDefinition;
            side = placement.Side;

            if (footprint != null)
            {
                footprint.localScale = placement.Size;
            }

            if (footprintCollider != null)
            {
                footprintCollider.size = placement.Size;
                footprintCollider.center = Vector3.zero;
            }

            ApplyColors();
            ApplyLabels();
            ApplyIcon();
            SetOwner(null);
            SetHouses(0);
            SetHighlight(false);
        }

        public void SetOwner(Color? ownerColor)
        {
            if (ownerMarkerRoot != null)
            {
                ownerMarkerRoot.SetActive(ownerColor.HasValue);
            }

            if (ownerColor.HasValue && ownerMarkerRenderer != null)
            {
                SetRendererColor(ownerMarkerRenderer, ownerColor.Value);
            }
        }

        public void SetHouses(int count)
        {
            if (housesRoot == null)
            {
                return;
            }

            for (int i = 0; i < housesRoot.childCount; i++)
            {
                housesRoot.GetChild(i).gameObject.SetActive(i < count);
            }
        }

        public void SetHighlight(bool highlighted)
        {
            if (highlightRoot != null)
            {
                highlightRoot.SetActive(highlighted);
            }
        }

        private void ApplyColors()
        {
            if (definition == null || template == null)
            {
                return;
            }

            BoardTypeStyle style = template.GetTypeStyle(definition.Type);
            Color baseColor = style != null ? style.BaseTint : Color.white;
            Color accentColor = ResolveAccent(style);

            if (baseRenderer != null)
            {
                baseRenderer.sharedMaterial = template.BaseMaterial;
                SetRendererColor(baseRenderer, baseColor);
            }

            if (accentRenderer != null)
            {
                accentRenderer.sharedMaterial = template.AccentMaterial;
                SetRendererColor(accentRenderer, accentColor);
            }

            if (iconRenderer != null)
            {
                iconRenderer.sharedMaterial = template.IconMaterial;
                SetRendererColor(iconRenderer, accentColor);
            }

            if (ownerMarkerRenderer != null)
            {
                ownerMarkerRenderer.sharedMaterial = template.OwnerMarkerMaterial;
            }
        }

        private Color ResolveAccent(BoardTypeStyle style)
        {
            if (definition.UseAccentOverride)
            {
                return definition.AccentOverride;
            }

            if (definition.IsProperty)
            {
                return template.GetGroupColor(definition.ColorGroup);
            }

            return style != null ? style.AccentTint : Color.white;
        }

        private void ApplyLabels()
        {
            if (template == null || definition == null)
            {
                return;
            }

            BoardTypeStyle style = template.GetTypeStyle(definition.Type);
            Color baseColor = style != null ? style.BaseTint : Color.white;
            Color labelColor = GetReadableTextColor(baseColor, template.TextColor);

            if (nameLabel != null)
            {
                bool showName = (style == null || style.ShowName) && !string.IsNullOrWhiteSpace(definition.DisplayName);
                nameLabel.gameObject.SetActive(showName);

                if (showName)
                {
                    nameLabel.text = definition.DisplayName;
                    nameLabel.fontSize = template.NameFontSize;
                    nameLabel.color = labelColor;
                    nameLabel.raycastTarget = false;

                    if (template.Font != null)
                    {
                        nameLabel.font = template.Font;
                    }
                }
            }

            if (priceLabel != null)
            {
                bool showPrice = style != null && style.ShowPrice && definition.Price > 0;
                priceLabel.gameObject.SetActive(showPrice);

                if (showPrice)
                {
                    priceLabel.text = definition.Price.ToString();
                    priceLabel.fontSize = template.PriceFontSize;
                    priceLabel.color = labelColor;
                    priceLabel.raycastTarget = false;

                    if (template.Font != null)
                    {
                        priceLabel.font = template.Font;
                    }
                }
            }
        }

        private static Color GetReadableTextColor(Color background, Color fallback)
        {
            float luminance = 0.2126f * background.r + 0.7152f * background.g + 0.0722f * background.b;
            return luminance > 0.55f
                ? new Color(0.08f, 0.08f, 0.08f, fallback.a)
                : fallback;
        }

        private void ApplyIcon()
        {
            if (iconRenderer == null || template == null || definition == null)
            {
                return;
            }

            BoardTypeStyle style = template.GetTypeStyle(definition.Type);
            bool showIcon = style != null && style.ShowIcon && template.IconMaterial != null;

            iconRenderer.gameObject.SetActive(showIcon);

            if (showIcon)
            {
                iconRenderer.sharedMaterial = template.IconMaterial;
                SetRendererColor(iconRenderer, ResolveAccent(style));
            }
        }

        private void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
