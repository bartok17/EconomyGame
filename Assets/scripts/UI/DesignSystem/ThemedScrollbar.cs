using MonopolyGame.DesignSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Applies style-sheet tokens to a <see cref="Scrollbar"/> track and handle.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Scrollbar))]
    [DisallowMultipleComponent]
    public sealed class ThemedScrollbar : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ImageRole    trackRole  = ImageRole.ScrollbarTrack;
        [SerializeField] private ImageRole    handleRole = ImageRole.ScrollbarHandle;

        private Scrollbar _scrollbar;
        private Image     _track;
        private Image     _handle;

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

            _scrollbar ??= GetComponent<Scrollbar>();
            _track     ??= GetComponent<Image>();

            if (_track != null)
                _track.color = sheet.GetImageColor(trackRole);

            if (_handle == null && _scrollbar != null && _scrollbar.handleRect != null)
                _handle = _scrollbar.handleRect.GetComponent<Image>();

            if (_handle != null)
                _handle.color = sheet.GetImageColor(handleRole);
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Style Now")]
        private void EditorApplyStyle() => ApplyStyle();
#endif
    }
}
