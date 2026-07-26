using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Builds a default runtime HUD layout when scene references are missing.
    /// All visual values (colors, sizes, spacing) are driven by a
    /// <see cref="UIStyleSheet"/> when provided; otherwise sensible defaults are used.
    /// </summary>
    public sealed class GameHudLayoutBuilder
    {
        // ── Fallback defaults (used when no UIStyleSheet is assigned) ──
        private const float FallbackPanelTopOffset       = 220f;
        private const float FallbackPanelHorizontalPad   = 24f;
        private const float FallbackElementSpacing       = 10f;
        private const float FallbackButtonRowSpacing     = 12f;
        private const float FallbackButtonWidth          = 120f;
        private const float FallbackButtonHeight         = 36f;
        private const float FallbackButtonFontSize       = 16f;
        private const float FallbackHighlightMultiplier  = 1.2f;
        private const float FallbackPressedMultiplier    = 0.8f;

        private static readonly Color FallbackPanelBg    = new(0.08f, 0.10f, 0.13f, 0.92f);
        private static readonly Color FallbackTitleColor = new(0.96f, 0.90f, 0.62f);
        private static readonly Color FallbackAccent     = new(0.80f, 0.88f, 1.00f);
        private static readonly Color FallbackDiceColor  = new(0.90f, 0.92f, 0.78f);
        private static readonly Color FallbackBalance    = new(0.76f, 1.00f, 0.76f);
        private static readonly Color FallbackProperty   = new(0.90f, 0.86f, 0.72f);
        private static readonly Color FallbackWarning    = new(1.00f, 0.86f, 0.58f);
        private static readonly Color FallbackMuted      = new(0.82f, 0.82f, 0.82f);
        private static readonly Color FallbackBtnPrimary = new(0.22f, 0.52f, 0.92f);
        private static readonly Color FallbackBtnDanger  = new(0.88f, 0.46f, 0.15f);
        private static readonly Color FallbackBtnSuccess = new(0.20f, 0.65f, 0.32f);

        public struct BuildResult
        {
            public Canvas RootCanvas;
            public EventSystem EventSystem;
            public TextMeshProUGUI TitleText;
            public TextMeshProUGUI TurnText;
            public TextMeshProUGUI PhaseText;
            public TextMeshProUGUI DiceText;
            public TextMeshProUGUI BalanceText;
            public TextMeshProUGUI PropertyText;
            public TextMeshProUGUI OwnerText;
            public TextMeshProUGUI EconomyMessageText;
            public TextMeshProUGUI HostHintText;
            public Button RollButton;
            public Button EndTurnButton;
            public Button BuyButton;
        }

        // ════════════════════════════════════════════════════════════
        //  Public API
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Build the HUD using the given stylesheet. Pass null to use hardcoded defaults.
        /// </summary>
        public BuildResult Build(UIStyleSheet styleSheet = null)
        {
            BuildResult result = new BuildResult();

            result.RootCanvas  = CreateRootCanvas();
            result.EventSystem = CreateEventSystem();

            Transform panel = CreatePanel(result.RootCanvas.transform, styleSheet);

            result.TitleText          = CreateThemedText(panel, "Multiplayer Turn HUD",
                                            TextStyle.Title,    styleSheet, FallbackTitleColor, 22);
            result.TurnText           = CreateThemedText(panel, "Turn: -",
                                            TextStyle.Heading,  styleSheet, Color.white,        20);
            result.PhaseText          = CreateThemedText(panel, "Phase: -",
                                            TextStyle.Accent,   styleSheet, FallbackAccent,      18);
            result.DiceText           = CreateThemedText(panel, "Dice: -",
                                            TextStyle.Dice,     styleSheet, FallbackDiceColor,   18);
            result.BalanceText        = CreateThemedText(panel, "Balance: -",
                                            TextStyle.Balance,  styleSheet, FallbackBalance,     18);
            result.PropertyText       = CreateThemedText(panel, "Property: -",
                                            TextStyle.Property, styleSheet, FallbackProperty,    18);
            result.OwnerText          = CreateThemedText(panel, "Owner: -",
                                            TextStyle.Accent,   styleSheet, FallbackAccent,      18);
            result.EconomyMessageText = CreateThemedText(panel, "No economy action yet.",
                                            TextStyle.Warning,  styleSheet, FallbackWarning,     16);
            result.HostHintText       = CreateThemedText(panel,
                                            "Host controls turn flow until client ownership is wired.",
                                            TextStyle.Muted,    styleSheet, FallbackMuted,       16);

            Transform actionsRow = CreateActionsRow(panel, styleSheet);

            result.RollButton    = CreateThemedButton(actionsRow, "Roll",
                                        ButtonRole.Primary, ButtonSize.Standard, styleSheet,
                                        FallbackBtnPrimary);
            result.EndTurnButton = CreateThemedButton(actionsRow, "End Turn",
                                        ButtonRole.Danger,  ButtonSize.Standard, styleSheet,
                                        FallbackBtnDanger);
            result.BuyButton     = CreateThemedButton(actionsRow, "Buy",
                                        ButtonRole.Success, ButtonSize.Standard, styleSheet,
                                        FallbackBtnSuccess);

            return result;
        }

        // ════════════════════════════════════════════════════════════
        //  Canvas / EventSystem
        // ════════════════════════════════════════════════════════════

        private static Canvas CreateRootCanvas()
        {
            GameObject canvasObject = new GameObject("GameHudCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static EventSystem CreateEventSystem()
        {
            GameObject obj = new GameObject("EventSystem");
            EventSystem es = obj.AddComponent<EventSystem>();
            obj.AddComponent<StandaloneInputModule>();
            return es;
        }

        // ════════════════════════════════════════════════════════════
        //  Panel
        // ════════════════════════════════════════════════════════════

        private static Transform CreatePanel(Transform parent, UIStyleSheet sheet)
        {
            float topOffset = sheet != null ? sheet.PanelTopOffset : FallbackPanelTopOffset;
            float horizPad  = sheet != null ? sheet.PanelHorizontalPadding : FallbackPanelHorizontalPad;
            float spacing   = sheet != null ? sheet.ElementSpacing : FallbackElementSpacing;
            RectOffset pad  = sheet != null ? sheet.PanelPadding : new RectOffset(16, 16, 16, 16);
            Color bgColor   = sheet != null ? sheet.GetImageColor(ImageRole.PanelBackground) : FallbackPanelBg;

            GameObject panel = new GameObject("GameHudPanel",
                typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(parent, false);

            RectTransform pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0f, 1f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot     = new Vector2(0.5f, 1f);
            pr.offsetMin = new Vector2(horizPad, -topOffset);
            pr.offsetMax = new Vector2(-horizPad, -horizPad);

            panel.GetComponent<Image>().color = bgColor;

            VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.spacing               = spacing;
            vlg.padding               = pad;
            vlg.childAlignment        = TextAnchor.UpperLeft;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth  = true;

            ContentSizeFitter csf = panel.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            return panel.transform;
        }

        // ════════════════════════════════════════════════════════════
        //  Actions row
        // ════════════════════════════════════════════════════════════

        private static Transform CreateActionsRow(Transform parent, UIStyleSheet sheet)
        {
            float spacing = sheet != null ? sheet.ButtonRowSpacing : FallbackButtonRowSpacing;

            GameObject row = new GameObject("ActionsRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing                = spacing;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth  = false;
            hlg.childAlignment         = TextAnchor.MiddleLeft;

            return row.transform;
        }

        // ════════════════════════════════════════════════════════════
        //  Themed helpers
        // ════════════════════════════════════════════════════════════

        private static TextMeshProUGUI CreateThemedText(
            Transform parent, string value, TextStyle style,
            UIStyleSheet sheet, Color fallbackColor, float fallbackSize)
        {
            GameObject obj = new GameObject("Label",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);

            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value;

            if (sheet != null)
            {
                if (sheet.DefaultFont != null)
                    text.font = sheet.DefaultFont;
                text.fontSize = sheet.GetTextSize(style);
                text.color    = sheet.GetTextColor(style);
            }
            else
            {
                text.fontSize = fallbackSize;
                text.color    = fallbackColor;
            }

            return text;
        }

        private static Button CreateThemedButton(
            Transform parent, string label,
            ButtonRole role, ButtonSize size, UIStyleSheet sheet,
            Color fallbackColor)
        {
            GameObject obj = new GameObject(label + "Button",
                typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);

            Image image    = obj.GetComponent<Image>();
            Button button  = obj.GetComponent<Button>();
            LayoutElement layout = obj.GetComponent<LayoutElement>();

            float btnWidth, btnHeight, fontSize, highlightMul, pressedMul;
            Color baseColor, textColor;

            if (sheet != null)
            {
                baseColor    = sheet.GetButtonColor(role);
                btnWidth     = sheet.GetButtonWidth(size);
                btnHeight    = sheet.GetButtonHeight(size);
                fontSize     = sheet.ButtonFontSize;
                textColor    = sheet.ButtonTextColor;
                highlightMul = sheet.ButtonHighlightMultiplier;
                pressedMul   = sheet.ButtonPressedMultiplier;
            }
            else
            {
                baseColor    = fallbackColor;
                btnWidth     = FallbackButtonWidth;
                btnHeight    = FallbackButtonHeight;
                fontSize     = FallbackButtonFontSize;
                textColor    = Color.white;
                highlightMul = FallbackHighlightMultiplier;
                pressedMul   = FallbackPressedMultiplier;
            }

            image.color = baseColor;

            ColorBlock cb = button.colors;
            cb.normalColor     = baseColor;
            cb.highlightedColor = baseColor * highlightMul;
            cb.pressedColor     = baseColor * pressedMul;
            cb.selectedColor    = baseColor;
            cb.disabledColor    = baseColor * 0.5f;
            button.colors = cb;

            layout.preferredWidth  = btnWidth;
            layout.preferredHeight = btnHeight;

            // -- Child label --
            GameObject textObj = new GameObject("Text",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(obj.transform, false);

            RectTransform tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            TextMeshProUGUI labelText = textObj.GetComponent<TextMeshProUGUI>();
            labelText.text      = label;
            labelText.fontSize  = fontSize;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color     = textColor;

            return button;
        }
    }
}
