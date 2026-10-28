using MonopolyGame.DesignSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class ThemedButton : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ButtonRole  buttonRole  = ButtonRole.Primary;
        [SerializeField] private ButtonSize  buttonSize  = ButtonSize.Standard;

        private Button          _button;
        private Image           _image;
        private TMPro.TextMeshProUGUI _label;

        // ── Unity lifecycle ────────────────────────────

        private void Awake()
        {
            ApplyStyle();
        }

        private void OnEnable()
        {
            ApplyStyle();
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        private void Reset()
        {
            ApplyStyle();
        }

        // ── Public API ─────────────────────────────────

        public void ApplyStyle()
        {
            UIStyleSheet sheet = styleSheet != null ? styleSheet : UIStyleSheetLocator.Instance;
            if (sheet == null) return;

            _image  ??= GetComponent<Image>();
            _button ??= GetComponent<Button>();
            _label  ??= GetComponentInChildren<TMPro.TextMeshProUGUI>();

            Color baseColor = sheet.GetButtonColor(buttonRole);

            // -- Image fill --
            if (_image != null)
                _image.color = baseColor;

            // -- Button color block --
            if (_button != null)
            {
                ColorBlock cb = _button.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor  = baseColor * sheet.ButtonHighlightMultiplier;
                cb.pressedColor      = baseColor * sheet.ButtonPressedMultiplier;
                cb.selectedColor     = baseColor;
                cb.disabledColor     = baseColor * 0.5f;
                _button.colors = cb;
            }

            // -- LayoutElement sizing --
            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth  = sheet.GetButtonWidth(buttonSize);
                layout.preferredHeight = sheet.GetButtonHeight(buttonSize);
            }

            // -- Child label --
            if (_label != null)
            {
                _label.color     = sheet.ButtonTextColor;
                _label.fontSize  = sheet.ButtonFontSize;
                _label.alignment = TMPro.TextAlignmentOptions.Center;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Style Now")]
        private void EditorApplyStyle()
        {
            ApplyStyle();
        }
#endif
    }
}
