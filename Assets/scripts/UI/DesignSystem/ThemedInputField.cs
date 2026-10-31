using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Applies style-sheet tokens to a <see cref="TMP_InputField"/>: background,
    /// typed text, placeholder, caret and selection.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_InputField))]
    [DisallowMultipleComponent]
    public sealed class ThemedInputField : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ImageRole    backgroundRole   = ImageRole.InputBackground;
        [SerializeField] private TextStyle    textStyle        = TextStyle.Body;
        [SerializeField] private TextStyle    placeholderStyle = TextStyle.Muted;

        private TMP_InputField        _input;
        private Image                 _background;
        private TextMeshProUGUI       _text;
        private TextMeshProUGUI       _placeholder;

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

            _input      ??= GetComponent<TMP_InputField>();
            _background ??= GetComponent<Image>();

            if (_background != null)
                _background.color = sheet.GetImageColor(backgroundRole);

            if (_text == null && _input != null && _input.textComponent is TextMeshProUGUI text)
                _text = text;

            if (_placeholder == null && _input != null && _input.placeholder is TextMeshProUGUI placeholder)
                _placeholder = placeholder;

            ApplyText(_text, sheet, textStyle);
            ApplyText(_placeholder, sheet, placeholderStyle);

            if (_input != null)
            {
                _input.caretColor     = sheet.CaretColor;
                _input.selectionColor = sheet.SelectionColor;
            }
        }

        private static void ApplyText(TextMeshProUGUI label, UIStyleSheet sheet, TextStyle style)
        {
            if (label == null) return;

            if (sheet.DefaultFont != null)
                label.font = sheet.DefaultFont;

            label.fontSize = sheet.GetTextSize(style);
            label.color    = sheet.GetTextColor(style);
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Style Now")]
        private void EditorApplyStyle() => ApplyStyle();
#endif
    }
}
