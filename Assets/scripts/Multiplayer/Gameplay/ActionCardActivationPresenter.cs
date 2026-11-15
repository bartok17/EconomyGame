using System.Collections.Generic;
using MonopolyGame.Cards;
using MonopolyGame.DesignSystem;
using MonopolyGame.Multiplayer.Camera;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Modal that turns a hand card into an activation: it renders one control
    /// per card parameter, keeps the selection in sync with pawn clicks on the
    /// table and previews the outcome before the player confirms.
    /// </summary>
    public sealed class ActionCardActivationPresenter : MonoBehaviour, ICardPreviewBoard
    {
        private const float ChipWidth = 140f;

        [Header("Dependencies")]
        [SerializeField] private BoardFocusCoordinator boardFocus;

        private readonly List<PlayerChip> chips = new List<PlayerChip>();

        private MultiplayerGameSessionController session;
        private RectTransform panelRoot;
        private CanvasGroup canvasGroup;

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI descriptionText;
        private TextMeshProUGUI targetLabel;
        private RectTransform chipsRow;
        private TextMeshProUGUI amountLabel;
        private TextMeshProUGUI amountValueText;
        private TextMeshProUGUI previewText;
        private TextMeshProUGUI errorText;
        private Button minusButton;
        private Button plusButton;
        private Button confirmButton;
        private TextMeshProUGUI confirmLabel;
        private Button closeButton;
        private GameObject amountRow;

        private ActionCardHandEntry entry;
        private ActionCardDefinition definition;
        private ActionCardParameters parameters;
        private ActionCardParameterSpec amountSpec;
        private bool hasPlayerParameter;
        private bool isOpen;

        public bool IsOpen => isOpen;

        // ── Lifecycle ───────────────────────────────────────────────────────────

        private void Awake()
        {
            panelRoot = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            BuildUi();
            SetVisible(false);
        }

        private void Update()
        {
            if (!isOpen || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void OnDisable()
        {
            UnsubscribeBoard();
        }

        public void BindSession(MultiplayerGameSessionController sessionController, BoardFocusCoordinator focusCoordinator)
        {
            session = sessionController;

            if (focusCoordinator != null)
            {
                boardFocus = focusCoordinator;
            }
        }

        // ── Open / close ────────────────────────────────────────────────────────

        public void Open(ActionCardHandEntry handEntry)
        {
            if (session == null)
            {
                return;
            }

            if (!ActionCardCatalog.TryGetDefinition(handEntry.CardId.ToString(), out ActionCardDefinition card))
            {
                Debug.LogWarning($"[ActionCards] Unknown card id '{handEntry.CardId}'.");
                return;
            }

            entry = handEntry;
            definition = card;
            parameters = BuildDefaultParameters(card);
            isOpen = true;

            titleText.text = card.DisplayName;
            descriptionText.text = card.Description;

            bool passive = card.Timing == CardTiming.Passive;

            hasPlayerParameter = TryGetSpec(ActionCardParameterKind.PlayerTarget, out _);
            amountSpec = TryGetSpec(ActionCardParameterKind.Amount, out ActionCardParameterSpec spec) ? spec : null;

            targetLabel.gameObject.SetActive(!passive && hasPlayerParameter);
            chipsRow.gameObject.SetActive(!passive && hasPlayerParameter);
            amountLabel.gameObject.SetActive(!passive && amountSpec != null);
            amountRow.SetActive(!passive && amountSpec != null);
            confirmButton.gameObject.SetActive(!passive);

            if (passive)
            {
                descriptionText.text = $"{card.Description}\n\nThis card triggers automatically.";
            }

            BuildChips();
            RefreshChips();
            RefreshAmount();
            RefreshPreview();

            if (passive)
            {
                previewText.text = "This card fires by itself when another player targets you.";
            }

            errorText.text = string.Empty;
            SetVisible(true);
            SubscribeBoard();
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            entry = default;
            definition = null;
            UnsubscribeBoard();
            SetVisible(false);
        }

        // ── UI construction ─────────────────────────────────────────────────────

        private void BuildUi()
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            var vertical = gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(24, 24, 20, 20);
            vertical.spacing = 10f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform header = CreateRow("Header", 36f);
            ConfigureHorizontal(header, spacing: 8f, expandWidth: true);

            titleText = CreateText(header, "Title", "Title", TextStyle.Title);
            SetFlexibleWidth(titleText.gameObject, 1f);
            closeButton = CreateButton(header, "Close", "X", ButtonRole.Neutral, new Vector2(32f, 32f), Close);

            descriptionText = CreateText(panelRoot, "Description", string.Empty, TextStyle.Muted);

            targetLabel = CreateText(panelRoot, "TargetLabel", "TARGET PLAYER", TextStyle.Caption);
            chipsRow = CreateRow("Chips", 68f);
            ConfigureHorizontal(chipsRow, spacing: 8f, expandWidth: false);

            amountLabel = CreateText(panelRoot, "AmountLabel", "AMOUNT", TextStyle.Caption);

            RectTransform amountRowRect = CreateRow("AmountRow", 44f);
            ConfigureHorizontal(amountRowRect, spacing: 12f, expandWidth: false);
            amountRow = amountRowRect.gameObject;

            minusButton = CreateButton(amountRowRect, "Minus", "\u2212", ButtonRole.Neutral, new Vector2(44f, 40f), HandleMinusClicked);
            amountValueText = CreateText(amountRowRect, "Value", "0", TextStyle.Heading);
            amountValueText.alignment = TextAlignmentOptions.Center;
            SetFlexibleWidth(amountValueText.gameObject, 1f);
            plusButton = CreateButton(amountRowRect, "Plus", "+", ButtonRole.Neutral, new Vector2(44f, 40f), HandlePlusClicked);

            previewText = CreateText(panelRoot, "Preview", string.Empty, TextStyle.Info);
            previewText.alignment = TextAlignmentOptions.Center;

            errorText = CreateText(panelRoot, "Error", string.Empty, TextStyle.Error);
            errorText.alignment = TextAlignmentOptions.Center;

            RectTransform buttons = CreateRow("Buttons", 48f);
            ConfigureHorizontal(buttons, spacing: 16f, expandWidth: false);
            CreateButton(buttons, "Cancel", "Cancel", ButtonRole.Neutral, new Vector2(160f, 44f), Close);
            confirmButton = CreateButton(buttons, "Confirm", "Activate", ButtonRole.Primary, new Vector2(160f, 44f), HandleConfirmClicked);
            confirmLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>();

            if (sheet != null)
            {
                var background = GetComponent<Image>();

                if (background != null)
                {
                    background.color = sheet.GetImageColor(ImageRole.PanelBackground);
                }
            }
        }

        /// <summary>Creates a layout-managed section with a fixed preferred height.</summary>
        private RectTransform CreateRow(string name, float height)
        {
            RectTransform rect = CreateChild(panelRoot, name);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return rect;
        }

        private static void ConfigureHorizontal(RectTransform rect, float spacing, bool expandWidth)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
        }

        private static void SetFlexibleWidth(GameObject target, float flexibleWidth)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();

            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.flexibleWidth = flexibleWidth;
        }

        private static RectTransform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content, TextStyle style)
        {
            RectTransform row = CreateChild(parent, name);
            var text = row.gameObject.AddComponent<TextMeshProUGUI>();
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            if (sheet != null)
            {
                sheet.ApplyTextStyle(text, style);
            }

            text.text = content;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.margin = new Vector4(2f, 2f, 2f, 2f);
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            ButtonRole role,
            Vector2 size,
            UnityAction onClick)
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            RectTransform rect = CreateChild(parent, name);

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = size.x;
            element.preferredWidth = size.x;
            element.minHeight = size.y;
            element.preferredHeight = size.y;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = sheet != null ? sheet.GetButtonColor(role) : Color.gray;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var text = CreateChild(rect, "Text");
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = Vector2.zero;
            text.offsetMax = Vector2.zero;

            var tmp = text.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.color = sheet != null ? sheet.ButtonTextColor : Color.white;
            tmp.fontSize = sheet != null ? sheet.ButtonFontSize : 16f;

            if (sheet != null && sheet.DefaultFont != null)
            {
                tmp.font = sheet.DefaultFont;
            }

            if (sheet != null)
            {
                sheet.ApplyOutline(tmp);
            }

            return button;
        }

        // ── Player chips ────────────────────────────────────────────────────────

        private void BuildChips()
        {
            for (int i = chips.Count - 1; i >= 0; i--)
            {
                if (chips[i].Root != null)
                {
                    Destroy(chips[i].Root.gameObject);
                }
            }

            chips.Clear();

            if (!hasPlayerParameter)
            {
                return;
            }

            IReadOnlyList<CardPlayerState> players = session.GetCardPlayers();
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            for (int i = 0; i < players.Count; i++)
            {
                CardPlayerState player = players[i];

                RectTransform rect = CreateChild(chipsRow, $"Chip{player.PawnSlot}");
                var element = rect.gameObject.AddComponent<LayoutElement>();
                element.minWidth = ChipWidth;
                element.preferredWidth = ChipWidth;
                element.minHeight = 64f;
                element.preferredHeight = 64f;

                var background = rect.gameObject.AddComponent<Image>();
                background.color = sheet != null ? sheet.GetImageColor(ImageRole.CardBackground) : Color.black;

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;

                int capturedSlot = player.PawnSlot;
                button.onClick.AddListener(() => HandleChipClicked(capturedSlot));

                TextMeshProUGUI name = CreateText(rect, "Name", player.Label, TextStyle.Body);
                name.alignment = TextAlignmentOptions.Center;
                name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                name.rectTransform.anchorMax = new Vector2(1f, 1f);
                name.rectTransform.offsetMin = new Vector2(4f, 0f);
                name.rectTransform.offsetMax = new Vector2(-4f, -6f);

                TextMeshProUGUI balance = CreateText(rect, "Balance", player.Balance.ToString(), TextStyle.Balance);
                balance.alignment = TextAlignmentOptions.Center;
                balance.rectTransform.anchorMin = new Vector2(0f, 0f);
                balance.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                balance.rectTransform.offsetMin = new Vector2(4f, 6f);
                balance.rectTransform.offsetMax = new Vector2(-4f, 0f);

                chips.Add(new PlayerChip
                {
                    Root = rect,
                    Background = background,
                    Name = name,
                    Balance = balance,
                    PawnSlot = player.PawnSlot
                });
            }
        }

        private void RefreshChips()
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;
            int selected = parameters.TargetPawnSlot;

            for (int i = 0; i < chips.Count; i++)
            {
                PlayerChip chip = chips[i];
                bool isSelected = chip.PawnSlot == selected;

                if (chip.Background != null)
                {
                    chip.Background.color = isSelected
                        ? (sheet != null ? sheet.GetPlayerColor(ToPlayerColorRole(chip.PawnSlot)) : Color.cyan)
                        : (sheet != null ? sheet.GetImageColor(ImageRole.CardBackground) : Color.black);
                }

                if (chip.Name != null)
                {
                    chip.Name.color = isSelected
                        ? (sheet != null ? sheet.ButtonTextColor : Color.white)
                        : (sheet != null ? sheet.GetTextColor(TextStyle.Body) : Color.white);
                }

                if (chip.Balance != null)
                {
                    chip.Balance.color = isSelected
                        ? (sheet != null ? sheet.ButtonTextColor : Color.white)
                        : (sheet != null ? sheet.GetTextColor(TextStyle.Balance) : Color.white);
                }
            }
        }

        private static PlayerColorRole ToPlayerColorRole(int pawnSlot)
        {
            return pawnSlot switch
            {
                1 => PlayerColorRole.Player2,
                2 => PlayerColorRole.Player3,
                3 => PlayerColorRole.Player4,
                _ => PlayerColorRole.Player1
            };
        }

        // ── Interaction ─────────────────────────────────────────────────────────

        private void HandleChipClicked(int pawnSlot)
        {
            parameters = parameters.WithTarget(pawnSlot);
            RefreshChips();
            RefreshPreview();
            errorText.text = string.Empty;
        }

        private void HandlePawnClicked(int pawnSlot)
        {
            if (!isOpen || !hasPlayerParameter)
            {
                return;
            }

            HandleChipClicked(pawnSlot);
        }

        private void HandleMinusClicked()
        {
            AdjustAmount(-1);
        }

        private void HandlePlusClicked()
        {
            AdjustAmount(1);
        }

        private void AdjustAmount(int direction)
        {
            if (amountSpec == null)
            {
                return;
            }

            int next = parameters.Amount + direction * amountSpec.Step;
            parameters = parameters.WithAmount(amountSpec.ClampAmount(next));
            RefreshAmount();
            RefreshPreview();
        }

        private void RefreshAmount()
        {
            if (amountSpec == null)
            {
                return;
            }

            amountLabel.text = amountSpec.Label.ToUpperInvariant();
            amountValueText.text = $"{parameters.Amount}{amountSpec.Suffix}";

            if (minusButton != null)
            {
                minusButton.interactable = parameters.Amount > amountSpec.Min;
            }

            if (plusButton != null)
            {
                plusButton.interactable = parameters.Amount < amountSpec.Max;
            }
        }

        private void RefreshPreview()
        {
            if (definition == null)
            {
                return;
            }

            IReadOnlyList<CardPlayerState> players = session.GetCardPlayers();
            string preview = definition.DescribePreview(parameters, players, this);
            previewText.text = preview;

            if (string.IsNullOrEmpty(preview))
            {
                previewText.text = hasPlayerParameter && parameters.HasTarget
                    ? "Ready to activate."
                    : "Pick a player to continue.";
            }
        }

        private void HandleConfirmClicked()
        {
            if (definition == null || session == null)
            {
                return;
            }

            IReadOnlyList<CardPlayerState> players = session.GetCardPlayers();

            if (!definition.TryValidate(session.GetLocalPawnSlot(), parameters, players, out string error))
            {
                errorText.text = error;
                return;
            }

            if (session.RequestPlayActionCard(entry.InstanceId, parameters))
            {
                Close();
                return;
            }

            errorText.text = "You cannot play a card right now.";
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private ActionCardParameters BuildDefaultParameters(ActionCardDefinition card)
        {
            int target = ActionCardParameters.NoTarget;
            int amount = 0;

            IReadOnlyList<ActionCardParameterSpec> specs = card.Parameters;

            for (int i = 0; i < specs.Count; i++)
            {
                ActionCardParameterSpec spec = specs[i];

                if (spec.Kind == ActionCardParameterKind.PlayerTarget)
                {
                    target = session != null ? session.GetLocalPawnSlot() : ActionCardParameters.NoTarget;
                }
                else if (spec.Kind == ActionCardParameterKind.Amount)
                {
                    amount = spec.DefaultValue;
                }
            }

            return new ActionCardParameters(target, amount);
        }

        private bool TryGetSpec(ActionCardParameterKind kind, out ActionCardParameterSpec spec)
        {
            spec = null;

            if (definition == null)
            {
                return false;
            }

            IReadOnlyList<ActionCardParameterSpec> specs = definition.Parameters;

            for (int i = 0; i < specs.Count; i++)
            {
                if (specs[i].Kind == kind)
                {
                    spec = specs[i];
                    return true;
                }
            }

            return false;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = visible;
                canvasGroup.interactable = visible;
            }
        }

        private void SubscribeBoard()
        {
            if (boardFocus != null)
            {
                boardFocus.PawnClicked -= HandlePawnClicked;
                boardFocus.PawnClicked += HandlePawnClicked;
            }
        }

        private void UnsubscribeBoard()
        {
            if (boardFocus != null)
            {
                boardFocus.PawnClicked -= HandlePawnClicked;
            }
        }

        // ── ICardPreviewBoard ───────────────────────────────────────────────────

        public int GetPlayerSpaceIndex(int pawnSlot)
        {
            return session != null ? session.GetPlayerSpaceIndex(pawnSlot) : -1;
        }

        public int NormalizeIndex(int spaceIndex)
        {
            return session != null ? session.NormalizeBoardIndex(spaceIndex) : spaceIndex;
        }

        public string GetSpaceName(int spaceIndex)
        {
            return session != null ? session.GetBoardSpaceName(spaceIndex) : string.Empty;
        }

        private sealed class PlayerChip
        {
            public RectTransform Root;
            public Image Background;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Balance;
            public int PawnSlot;
        }
    }
}
