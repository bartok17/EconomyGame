using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// One physical action-card pile on the table. Cards are thin cubes stacked
    /// upward; the root collider is the future hover target for pile info.
    /// </summary>
    internal sealed class BoardCardPileView
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const float CardThickness = 0.035f;
        private const float CardGap = 0.006f;
        private const int MaxVisibleCards = 12;

        private readonly List<GameObject> cards = new List<GameObject>();
        private readonly Transform root;
        private readonly Transform cardsRoot;
        private readonly TextMeshPro countLabel;
        private readonly Material cardMaterial;
        private readonly Color cardColor;
        private readonly float cardWidth;
        private readonly float cardDepth;
        private readonly string caption;
        private readonly BoxCollider hitCollider;

        private float stackHeight;

        public BoardCardPileView(
            Transform parent,
            Vector3 localPosition,
            string caption,
            float labelSide,
            float cardWidth,
            float cardDepth,
            TMP_FontAsset font,
            Material cardMaterial,
            Color cardColor)
        {
            this.caption = caption ?? string.Empty;
            this.cardWidth = cardWidth;
            this.cardDepth = cardDepth;
            this.cardMaterial = cardMaterial;
            this.cardColor = cardColor;

            var rootObject = new GameObject($"{this.caption}Pile");
            root = rootObject.transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            cardsRoot = new GameObject("Cards").transform;
            cardsRoot.SetParent(root, false);

            countLabel = CreateLabel(root, font, labelSide);
            hitCollider = rootObject.AddComponent<BoxCollider>();
            SetCount(0);
        }

        public void SetCount(int count)
        {
            int visibleCards = Mathf.Clamp(count, 0, MaxVisibleCards);
            EnsureCards(visibleCards);

            for (int i = 0; i < cards.Count; i++)
            {
                bool visible = i < visibleCards;
                cards[i].SetActive(visible);

                if (visible)
                {
                    cards[i].transform.localPosition = new Vector3(
                        0f,
                        CardThickness * 0.5f + i * (CardThickness + CardGap),
                        0f);
                }
            }

            stackHeight = visibleCards * (CardThickness + CardGap);
            countLabel.text = $"{caption}\n{count}";
            UpdateCollider();
        }

        private void EnsureCards(int count)
        {
            while (cards.Count < count)
            {
                int index = cards.Count;
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Cube);
                card.name = $"Card_{index:00}";
                card.transform.SetParent(cardsRoot, false);
                card.transform.localScale = new Vector3(cardWidth, CardThickness, cardDepth);
                RemoveCollider(card);
                ConfigureRenderer(card.GetComponent<Renderer>());
                cards.Add(card);
            }
        }

        private void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (cardMaterial != null)
            {
                renderer.sharedMaterial = cardMaterial;
            }

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, cardColor);
            block.SetColor(ColorId, cardColor);
            renderer.SetPropertyBlock(block);
        }

        private TextMeshPro CreateLabel(Transform parent, TMP_FontAsset font, float labelSide)
        {
            var labelObject = new GameObject("CountLabel", typeof(RectTransform), typeof(TextMeshPro));
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(
                labelSide * 1.55f,
                0.015f,
                0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var label = labelObject.GetComponent<TextMeshPro>();

            if (font != null)
            {
                label.font = font;
            }

            label.text = caption;
            label.fontSize = 4.5f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 2.5f;
            label.fontSizeMax = 5.5f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(1.8f, 1.0f);
            label.ForceMeshUpdate();
            return label;
        }

        private void UpdateCollider()
        {
            float height = Mathf.Max(stackHeight, 0.03f);
            hitCollider.size = new Vector3(cardWidth + 0.08f, height + 0.04f, cardDepth + 0.08f);
            hitCollider.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();

            if (collider == null)
            {
                return;
            }

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
}
