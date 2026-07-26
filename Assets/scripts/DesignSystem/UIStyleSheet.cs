using UnityEngine;

namespace MonopolyGame.DesignSystem
{
    /// <summary>
    /// Semantic text roles. Add new entries here to extend the design system
    /// without touching any component code — just assign the new role in the Inspector.
    /// </summary>
    public enum TextStyle
    {
        Title,
        Heading,
        Body,
        Caption,
        Muted,
        Accent,
        Success,
        Warning,
        Balance,
        Property,
        Dice
    }

    /// <summary>
    /// Semantic button roles. Each role drives color, and sizing is driven by
    /// <see cref="ButtonSize"/>.
    /// </summary>
    public enum ButtonRole
    {
        Primary,
        Danger,
        Success,
        Neutral
    }

    /// <summary>
    /// Sizing presets for buttons — keeps width/height consistent across roles.
    /// </summary>
    public enum ButtonSize
    {
        Standard,
        Wide,
        Compact
    }

    /// <summary>
    /// Semantic image roles for panels, backgrounds, and decorative elements.
    /// </summary>
    public enum ImageRole
    {
        PanelBackground,
        CardBackground,
        Overlay
    }

    /// <summary>
    /// Central design-token store. Create one asset via
    /// Assets → Create → Monopoly Game → UI Style Sheet and assign it to all
    /// <see cref="ThemedText"/>, <see cref="ThemedButton"/>, and <see cref="ThemedImage"/>
    /// components. Changing a value here propagates everywhere.
    /// </summary>
    [CreateAssetMenu(menuName = "Monopoly Game/UI Style Sheet", fileName = "UIStyleSheet")]
    public sealed class UIStyleSheet : ScriptableObject
    {
        // ──────────────────────────────────────────────
        //  Font
        // ──────────────────────────────────────────────
        [Header("Font")]
        [SerializeField] private TMPro.TMP_FontAsset defaultFont;

        // ──────────────────────────────────────────────
        //  Text Colors (by TextStyle)
        // ──────────────────────────────────────────────
        [Header("Text Colors")]
        [SerializeField] private Color titleColor      = new(0.96f, 0.90f, 0.62f);
        [SerializeField] private Color headingColor     = Color.white;
        [SerializeField] private Color bodyColor        = Color.white;
        [SerializeField] private Color captionColor     = new(0.82f, 0.82f, 0.82f);
        [SerializeField] private Color mutedColor       = new(0.82f, 0.82f, 0.82f);
        [SerializeField] private Color accentColor      = new(0.80f, 0.88f, 1.00f);
        [SerializeField] private Color successColor     = new(0.76f, 1.00f, 0.76f);
        [SerializeField] private Color warningColor     = new(1.00f, 0.86f, 0.58f);
        [SerializeField] private Color balanceColor     = new(0.76f, 1.00f, 0.76f);
        [SerializeField] private Color propertyColor    = new(0.90f, 0.86f, 0.72f);
        [SerializeField] private Color diceColor        = new(0.90f, 0.92f, 0.78f);

        // ──────────────────────────────────────────────
        //  Text Sizes (by TextStyle)
        // ──────────────────────────────────────────────
        [Header("Text Sizes")]
        [SerializeField] private float titleSize    = 22f;
        [SerializeField] private float headingSize  = 20f;
        [SerializeField] private float bodySize     = 18f;
        [SerializeField] private float captionSize  = 16f;
        [SerializeField] private float mutedSize    = 16f;
        [SerializeField] private float accentSize   = 18f;
        [SerializeField] private float successSize  = 18f;
        [SerializeField] private float warningSize  = 16f;
        [SerializeField] private float balanceSize  = 18f;
        [SerializeField] private float propertySize = 18f;
        [SerializeField] private float diceSize     = 18f;

        // ──────────────────────────────────────────────
        //  Button Colors (by ButtonRole)
        // ──────────────────────────────────────────────
        [Header("Button Colors")]
        [SerializeField] private Color buttonPrimaryColor  = new(0.22f, 0.52f, 0.92f);
        [SerializeField] private Color buttonDangerColor   = new(0.88f, 0.46f, 0.15f);
        [SerializeField] private Color buttonSuccessColor  = new(0.20f, 0.65f, 0.32f);
        [SerializeField] private Color buttonNeutralColor  = new(0.35f, 0.35f, 0.40f);
        [SerializeField] private Color buttonTextColor     = Color.white;

        // ──────────────────────────────────────────────
        //  Button Sizing (by ButtonSize)
        // ──────────────────────────────────────────────
        [Header("Button Sizing")]
        [SerializeField] private float buttonStandardWidth   = 120f;
        [SerializeField] private float buttonStandardHeight  = 36f;
        [SerializeField] private float buttonWideWidth       = 180f;
        [SerializeField] private float buttonWideHeight      = 44f;
        [SerializeField] private float buttonCompactWidth    = 80f;
        [SerializeField] private float buttonCompactHeight   = 28f;
        [SerializeField] private float buttonFontSize        = 16f;
        [SerializeField] private float buttonHighlightMultiplier  = 1.2f;
        [SerializeField] private float buttonPressedMultiplier    = 0.8f;

        // ──────────────────────────────────────────────
        //  Panel & Layout
        // ──────────────────────────────────────────────
        [Header("Image Colors")]
        [SerializeField] private Color panelBackgroundColor  = new(0.08f, 0.10f, 0.13f, 0.92f);
        [SerializeField] private Color cardBackgroundColor   = new(0.10f, 0.12f, 0.15f, 0.88f);
        [SerializeField] private Color overlayColor          = new(0.00f, 0.00f, 0.00f, 0.60f);

        [Header("Layout — Panel")]
        [SerializeField] private float panelTopOffset           = 220f;
        [SerializeField] private float panelHorizontalPadding   = 24f;
        [SerializeField] private float elementSpacing           = 10f;
        [SerializeField] private float buttonRowSpacing         = 12f;
        [SerializeField] private RectOffset panelPadding = new(16, 16, 16, 16);

        // ══════════════════════════════════════════════
        //  Public accessors
        // ══════════════════════════════════════════════

        public TMPro.TMP_FontAsset DefaultFont => defaultFont;

        // -- Text --------------------------------------------------

        public Color GetTextColor(TextStyle style) => style switch
        {
            TextStyle.Title     => titleColor,
            TextStyle.Heading   => headingColor,
            TextStyle.Body      => bodyColor,
            TextStyle.Caption   => captionColor,
            TextStyle.Muted     => mutedColor,
            TextStyle.Accent    => accentColor,
            TextStyle.Success   => successColor,
            TextStyle.Warning   => warningColor,
            TextStyle.Balance   => balanceColor,
            TextStyle.Property  => propertyColor,
            TextStyle.Dice      => diceColor,
            _                   => bodyColor
        };

        public float GetTextSize(TextStyle style) => style switch
        {
            TextStyle.Title     => titleSize,
            TextStyle.Heading   => headingSize,
            TextStyle.Body      => bodySize,
            TextStyle.Caption   => captionSize,
            TextStyle.Muted     => mutedSize,
            TextStyle.Accent    => accentSize,
            TextStyle.Success   => successSize,
            TextStyle.Warning   => warningSize,
            TextStyle.Balance   => balanceSize,
            TextStyle.Property  => propertySize,
            TextStyle.Dice      => diceSize,
            _                   => bodySize
        };

        // -- Buttons -----------------------------------------------

        public Color GetButtonColor(ButtonRole role) => role switch
        {
            ButtonRole.Primary  => buttonPrimaryColor,
            ButtonRole.Danger   => buttonDangerColor,
            ButtonRole.Success  => buttonSuccessColor,
            ButtonRole.Neutral  => buttonNeutralColor,
            _                   => buttonPrimaryColor
        };

        public float GetButtonWidth(ButtonSize size) => size switch
        {
            ButtonSize.Standard => buttonStandardWidth,
            ButtonSize.Wide     => buttonWideWidth,
            ButtonSize.Compact  => buttonCompactWidth,
            _                   => buttonStandardWidth
        };

        public float GetButtonHeight(ButtonSize size) => size switch
        {
            ButtonSize.Standard => buttonStandardHeight,
            ButtonSize.Wide     => buttonWideHeight,
            ButtonSize.Compact  => buttonCompactHeight,
            _                   => buttonStandardHeight
        };

        public Color ButtonTextColor          => buttonTextColor;
        public float ButtonFontSize           => buttonFontSize;
        public float ButtonHighlightMultiplier => buttonHighlightMultiplier;
        public float ButtonPressedMultiplier   => buttonPressedMultiplier;

        // -- Images ------------------------------------------------

        public Color GetImageColor(ImageRole role) => role switch
        {
            ImageRole.PanelBackground => panelBackgroundColor,
            ImageRole.CardBackground  => cardBackgroundColor,
            ImageRole.Overlay         => overlayColor,
            _                         => panelBackgroundColor
        };

        // -- Layout ------------------------------------------------

        public float PanelTopOffset         => panelTopOffset;
        public float PanelHorizontalPadding => panelHorizontalPadding;
        public float ElementSpacing         => elementSpacing;
        public float ButtonRowSpacing       => buttonRowSpacing;
        public RectOffset PanelPadding      => panelPadding;
    }
}
