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
        [SerializeField] private bool        styleLabel  = true;

        private Button          _button;
        private Image           _image;
        private TMPro.TextMeshProUGUI _label;

        public ButtonRole Role => buttonRole;
        public ButtonSize Size => buttonSize;

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

        public void SetRole(ButtonRole role)
        {
            buttonRole = role;
            ApplyStyle();
        }

        public void SetSize(ButtonSize size)
        {
            buttonSize = size;
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            UIStyleSheet sheet = styleSheet != null ? styleSheet : UIStyleSheetLocator.Instance;
            if (sheet == null) return;

            _image  ??= GetComponent<Image>();
            _button ??= GetComponent<Button>();

            Color baseColor = sheet.GetButtonColor(buttonRole);

            // -- Image fill --
            // The fill stays white; the token lives in the ColorBlock so the
            // Selectable tint is applied exactly once (Image.color * tint).
            if (_image != null)
                _image.color = Color.white;

            // -- Button color block --
            if (_button != null)
            {
                ColorBlock cb = _button.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor = baseColor * sheet.ButtonHighlightMultiplier;
                cb.pressedColor     = baseColor * sheet.ButtonPressedMultiplier;
                cb.selectedColor    = baseColor;
                cb.disabledColor    = baseColor * sheet.ButtonDisabledMultiplier;
                _button.colors = cb;
            }

            ApplySize(sheet);

            // -- Child label --
            if (styleLabel)
            {
                _label ??= FindDirectChildLabel();

                if (_label != null)
                {
                    if (sheet.DefaultFont != null)
                        _label.font = sheet.DefaultFont;

                    _label.color     = sheet.ButtonTextColor;
                    _label.fontSize  = sheet.ButtonFontSize;
                    _label.alignment = TMPro.TextAlignmentOptions.Center;
                }
            }
        }

        private void ApplySize(UIStyleSheet sheet)
        {
            float width  = sheet.GetButtonWidth(buttonSize);
            float height = sheet.GetButtonHeight(buttonSize);

            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth  = width;
                layout.preferredHeight = height;
                return;
            }

            // No layout element -> only resize point-anchored buttons.
            // Stretched buttons own their rect through their anchors.
            if (transform is not RectTransform rect) return;
            if (rect.anchorMin != rect.anchorMax) return;

            rect.sizeDelta = new Vector2(width, height);
        }

        private TMPro.TextMeshProUGUI FindDirectChildLabel()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).TryGetComponent(out TMPro.TextMeshProUGUI label))
                    return label;
            }

            return null;
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
