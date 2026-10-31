using MonopolyGame.DesignSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.UI.DesignSystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class ThemedImage : MonoBehaviour
    {
        [SerializeField] private UIStyleSheet styleSheet;
        [SerializeField] private ImageRole    imageRole = ImageRole.PanelBackground;
        [SerializeField] private bool         usePlayerColor;
        [SerializeField] private PlayerColorRole playerColorRole = PlayerColorRole.Player1;

        private Image _image;

        public ImageRole Role => imageRole;

        /// <summary>Switch to a player colour token; pass <c>null</c> to go back to the image role.</summary>
        public void SetPlayerColor(PlayerColorRole? role)
        {
            usePlayerColor = role.HasValue;
            if (role.HasValue)
                playerColorRole = role.Value;

            ApplyStyle();
        }

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

        public void SetImageRole(ImageRole role)
        {
            imageRole = role;
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            UIStyleSheet sheet = styleSheet != null ? styleSheet : UIStyleSheetLocator.Instance;
            if (sheet == null) return;

            _image ??= GetComponent<Image>();
            if (_image == null) return;

            _image.color = usePlayerColor
                ? sheet.GetPlayerColor(playerColorRole)
                : sheet.GetImageColor(imageRole);
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
