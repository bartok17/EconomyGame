using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;

namespace MonopolyGame.UI.DesignSystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(TextMeshProUGUI))]
    [DisallowMultipleComponent]
    public sealed class ThemedText : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private TextStyle textStyle = TextStyle.Body;

        private TextMeshProUGUI _text;

        public TextStyle Style => textStyle;

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

        public void SetTextStyle(TextStyle style)
        {
            textStyle = style;
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            UIStyleSheet sheet = styleSheet != null ? styleSheet : UIStyleSheetLocator.Instance;
            if (sheet == null) return;

            _text ??= GetComponent<TextMeshProUGUI>();
            if (_text == null) return;

            sheet.ApplyTextStyle(_text, textStyle);
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
