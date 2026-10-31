using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Applies style-sheet tokens to a <see cref="TMP_Dropdown"/>: background,
    /// caption, item labels, item highlight and arrow.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Dropdown))]
    [DisallowMultipleComponent]
    public sealed class ThemedDropdown : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ImageRole    backgroundRole = ImageRole.DropdownBackground;
        [SerializeField] private TextStyle    labelStyle     = TextStyle.Body;
        [SerializeField] private TextStyle    itemLabelStyle = TextStyle.Body;
        [SerializeField] private Image        arrowImage;

        private TMP_Dropdown  _dropdown;
        private Image         _background;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _itemText;
        private Toggle        _itemToggle;

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

            _dropdown   ??= GetComponent<TMP_Dropdown>();
            _background ??= GetComponent<Image>();

            // Fill stays white; the token lives in the ColorBlock.
            if (_background != null)
                _background.color = Color.white;

            if (_dropdown != null)
            {
                Color baseColor = sheet.GetImageColor(backgroundRole);
                ColorBlock cb = _dropdown.colors;
                cb.normalColor      = baseColor;
                cb.highlightedColor = baseColor * sheet.ButtonHighlightMultiplier;
                cb.pressedColor     = baseColor * sheet.ButtonPressedMultiplier;
                cb.selectedColor    = baseColor;
                cb.disabledColor    = baseColor * sheet.ButtonDisabledMultiplier;
                _dropdown.colors = cb;

                if (_caption == null && _dropdown.captionText is TextMeshProUGUI caption)
                    _caption = caption;

                if (_itemText == null && _dropdown.itemText is TextMeshProUGUI itemText)
                    _itemText = itemText;
            }

            ApplyText(_caption, sheet, labelStyle);
            ApplyText(_itemText, sheet, itemLabelStyle);
            ApplyItemTemplate(sheet);
            ApplyArrow(sheet);
        }

        private void ApplyItemTemplate(UIStyleSheet sheet)
        {
            if (_dropdown == null || _dropdown.template == null) return;

            Image itemImage = _dropdown.itemImage;
            if (itemImage != null)
                itemImage.color = Color.white;

            if (_itemToggle == null)
                _itemToggle = _dropdown.template.GetComponentInChildren<Toggle>(true);

            if (_itemToggle != null)
            {
                ColorBlock cb = _itemToggle.colors;
                cb.normalColor      = sheet.GetImageColor(ImageRole.DropdownItemBackground);
                cb.highlightedColor = sheet.GetImageColor(ImageRole.DropdownItemHighlight);
                cb.pressedColor     = sheet.GetImageColor(ImageRole.DropdownItemHighlight);
                cb.selectedColor    = sheet.GetImageColor(ImageRole.DropdownItemBackground);
                _itemToggle.colors = cb;
            }
        }

        private void ApplyArrow(UIStyleSheet sheet)
        {
            if (arrowImage == null)
            {
                Transform arrow = transform.Find("Arrow");
                if (arrow != null)
                    arrowImage = arrow.GetComponent<Image>();
            }

            if (arrowImage != null)
                arrowImage.color = sheet.DropdownArrowColor;
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
