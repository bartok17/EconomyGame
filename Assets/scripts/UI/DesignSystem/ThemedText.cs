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

            _text ??= GetComponent<TextMeshProUGUI>();
            if (_text == null) return;

            if (styleSheet.DefaultFont != null)
                _text.font = styleSheet.DefaultFont;

            _text.fontSize = styleSheet.GetTextSize(textStyle);
            _text.color    = styleSheet.GetTextColor(textStyle);
        }

        // ── Context menu (editor convenience) ──────────

#if UNITY_EDITOR
        [ContextMenu("Apply Style Now")]
        private void EditorApplyStyle()
        {
            ApplyStyle();
        }
#endif
    }
}
