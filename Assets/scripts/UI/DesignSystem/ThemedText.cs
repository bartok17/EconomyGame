using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Drop this on any GameObject with a <see cref="TextMeshProUGUI"/>.
    /// Assign a <see cref="UIStyleSheet"/> and pick a <see cref="TextStyle"/> —
    /// font, size, and color are applied automatically at edit time and runtime.
    ///
    /// <para>
    /// To add a new text style: add an entry to the <see cref="TextStyle"/> enum
    /// and the corresponding color/size fields in <see cref="UIStyleSheet"/>.
    /// No code changes needed here.
    /// </para>
    /// </summary>
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

        /// <summary>
        /// Re-applies font, size, and color from the current <see cref="UIStyleSheet"/>.
        /// Safe to call repeatedly; does nothing if references are missing.
        /// </summary>
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
