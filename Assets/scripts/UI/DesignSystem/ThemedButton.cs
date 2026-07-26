using MonopolyGame.DesignSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Drop this on any GameObject with a <see cref="Button"/> and <see cref="Image"/>.
    /// Assign a <see cref="UIStyleSheet"/>, pick a <see cref="ButtonRole"/> and
    /// <see cref="ButtonSize"/> — colors, sizing, and text styling are applied
    /// automatically at edit time and runtime.
    ///
    /// <para>
    /// The component looks for a child <see cref="TMPro.TextMeshProUGUI"/> named "Text"
    /// (or the first TMP found) and styles its color / font size from the stylesheet.
    /// </para>
    /// </summary>
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
            if (styleSheet == null) return;

            _image  ??= GetComponent<Image>();
            _button ??= GetComponent<Button>();
            _label  ??= GetComponentInChildren<TMPro.TextMeshProUGUI>();

            Color baseColor = styleSheet.GetButtonColor(buttonRole);

            // -- Image fill --
            if (_image != null)
                _image.color = baseColor;

            // -- Button color block --
            if (_button != null)
            {
                ColorBlock cb = _button.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor  = baseColor * styleSheet.ButtonHighlightMultiplier;
                cb.pressedColor      = baseColor * styleSheet.ButtonPressedMultiplier;
                cb.selectedColor     = baseColor;
                cb.disabledColor     = baseColor * 0.5f;
                _button.colors = cb;
            }

            // -- LayoutElement sizing --
            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth  = styleSheet.GetButtonWidth(buttonSize);
                layout.preferredHeight = styleSheet.GetButtonHeight(buttonSize);
            }

            // -- Child label --
            if (_label != null)
            {
                _label.color     = styleSheet.ButtonTextColor;
                _label.fontSize  = styleSheet.ButtonFontSize;
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
