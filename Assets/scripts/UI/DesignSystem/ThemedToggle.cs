using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Applies style-sheet tokens to a <see cref="Toggle"/>: background,
    /// checkmark and (optionally) a label. The label is opt-in so it never
    /// fights a <see cref="ThemedText"/> on the same child.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Toggle))]
    [DisallowMultipleComponent]
    public sealed class ThemedToggle : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ImageRole    backgroundRole = ImageRole.InputBackground;
        [SerializeField] private TextStyle    labelStyle     = TextStyle.Caption;
        [SerializeField] private TextMeshProUGUI label;

        private Toggle _toggle;
        private Image  _background;
        private Image  _checkmark;

        // ── Unity lifecycle ────────────────────────────

        private void Awake()    => ApplyStyle();
        private void OnEnable() => ApplyStyle();
        private void OnValidate() => ApplyStyle();
        private void Reset()    => ApplyStyle();

        // ── Public API ─────────────────────────────────

        public void ApplyStyle()
        {
            UIStyleSheet sheet = styleSheet != null ? styleSheet : UIStyleSheetLocator.Instance;
            if (sheet == null) return;

            _toggle     ??= GetComponent<Toggle>();
            _background ??= GetComponent<Image>();

            // Fill stays white; the token lives in the ColorBlock.
            if (_background != null)
                _background.color = Color.white;

            if (_toggle != null)
            {
                Color baseColor = sheet.GetImageColor(backgroundRole);
                ColorBlock cb = _toggle.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor = baseColor * sheet.ButtonHighlightMultiplier;
                cb.pressedColor     = baseColor * sheet.ButtonPressedMultiplier;
                cb.selectedColor    = baseColor;
                cb.disabledColor    = baseColor * sheet.ButtonDisabledMultiplier;
                _toggle.colors = cb;

                if (_checkmark == null && _toggle.graphic is Image checkmark)
                    _checkmark = checkmark;
            }

            if (_checkmark != null)
                _checkmark.color = sheet.ToggleCheckmarkColor;

            if (label != null)
            {
                if (sheet.DefaultFont != null)
                    label.font = sheet.DefaultFont;

                label.fontSize = sheet.GetTextSize(labelStyle);
                label.color    = sheet.GetTextColor(labelStyle);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Style Now")]
        private void EditorApplyStyle() => ApplyStyle();
#endif
    }
}
