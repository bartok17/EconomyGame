using MonopolyGame.DesignSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    /// <summary>
    /// Drop this on any GameObject with an <see cref="Image"/>.
    /// Assign a <see cref="UIStyleSheet"/> and pick an <see cref="ImageRole"/> —
    /// the color is applied automatically at edit time and runtime.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class ThemedImage : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ImageRole    imageRole = ImageRole.PanelBackground;

        private Image _image;

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

            _image ??= GetComponent<Image>();
            if (_image == null) return;

            _image.color = styleSheet.GetImageColor(imageRole);
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
