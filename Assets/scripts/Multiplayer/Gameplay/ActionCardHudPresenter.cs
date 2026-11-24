using System;
using System.Collections.Generic;
using MonopolyGame.Cards;
using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Passive HUD view for the local player's action-card hand. Slots are
    /// buttons: clicking an active card opens <see cref="ActionCardActivationPresenter"/>.
    /// The draw and discard piles are represented physically on the board.
    /// </summary>
    public sealed class ActionCardHudPresenter : MonoBehaviour
    {
        [Header("Hand")]
        [SerializeField] private Transform handSlotsRoot;
        [SerializeField] private TextMeshProUGUI handHeaderText;

        public event Action<ActionCardHandEntry> CardActivated;

        private readonly List<SlotView> slots = new List<SlotView>();

        private MultiplayerGameSessionController session;
        private bool subscribed;
        private Color filledTextColor = Color.white;
        private Color emptyTextColor = new Color(0.65f, 0.65f, 0.65f);
        private Color activeCardColor = new Color(0.14f, 0.16f, 0.2f, 0.92f);
        private Color passiveCardColor = new Color(0.16f, 0.24f, 0.34f, 0.95f);

        private void Awake()
        {
            ResolveColors();
            ResolveHandSlots();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void BindSession(MultiplayerGameSessionController sessionController)
        {
            if (session == sessionController)
            {
                Refresh();
                return;
            }

            Unsubscribe();
            session = sessionController;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        private void HandleActionCardsChanged()
        {
            Refresh();
        }

        private void HandleTurnStateChanged()
        {
            RefreshInteractable();
        }

        private void Subscribe()
        {
            if (subscribed || session == null)
            {
                return;
            }

            session.ActionCardsChanged += HandleActionCardsChanged;
            session.PhaseChanged += HandlePhaseChanged;
            session.TurnChanged += HandleTurnChanged;
            session.CardResolutionChanged += HandleTurnStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || session == null)
            {
                return;
            }

            session.ActionCardsChanged -= HandleActionCardsChanged;
            session.PhaseChanged -= HandlePhaseChanged;
            session.TurnChanged -= HandleTurnChanged;
            session.CardResolutionChanged -= HandleTurnStateChanged;
            subscribed = false;
        }

        private void HandlePhaseChanged(TurnPhase _)
        {
            RefreshInteractable();
        }

        private void HandleTurnChanged(int _, string __)
        {
            RefreshInteractable();
        }

        private void Refresh()
        {
            ResolveHandSlots();

            int capacity = session != null
                ? session.ActionCardHandCapacity
                : ActionCardHand.DefaultCapacity;
            IReadOnlyList<ActionCardHandEntry> hand = session != null
                ? session.GetLocalActionCardHand()
                : null;
            int handCount = hand != null ? hand.Count : 0;

            if (handHeaderText != null)
            {
                handHeaderText.text = session != null
                    ? $"ACTION CARDS ({handCount}/{capacity})"
                    : "ACTION CARDS";
            }

            for (int i = 0; i < slots.Count; i++)
            {
                SlotView slot = slots[i];

                if (slot.Label == null)
                {
                    continue;
                }

                bool occupied = hand != null && i < hand.Count;
                slot.Entry = occupied ? hand[i] : default;
                slot.HasCard = occupied;
                slot.Definition = occupied ? ResolveDefinition(hand[i]) : null;
                slot.Label.text = occupied ? hand[i].ShortLabel.ToString() : "?";
                slot.Label.color = occupied ? filledTextColor : emptyTextColor;

                if (slot.Background != null)
                {
                    slot.Background.color = occupied && !slot.IsActiveCard
                        ? passiveCardColor
                        : activeCardColor;
                }
            }

            RefreshInteractable();
        }

        private void RefreshInteractable()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                SlotView slot = slots[i];
                bool playable = session != null && slot.IsActiveCard && slot.HasCard &&
                                session.IsActionCardWindowOpen(slot.ActivationWindow);

                if (slot.Button != null)
                {
                    slot.Button.interactable = playable;
                }

                if (slot.HasCard && slot.Label != null)
                {
                    slot.Label.color = playable ? filledTextColor : emptyTextColor;
                }
            }
        }

        private static ActionCardDefinition ResolveDefinition(ActionCardHandEntry entry)
        {
            return ActionCardCatalog.TryGetDefinition(entry.CardId.ToString(), out ActionCardDefinition definition)
                ? definition
                : null;
        }

        private void HandleSlotClicked(int index)
        {
            if (index < 0 || index >= slots.Count)
            {
                return;
            }

            SlotView slot = slots[index];

            if (!slot.HasCard || !slot.IsActiveCard)
            {
                return;
            }

            CardActivated?.Invoke(slot.Entry);
        }

        private void ResolveColors()
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            if (sheet == null)
            {
                return;
            }

            filledTextColor = sheet.GetTextColor(TextStyle.Body);
            emptyTextColor = sheet.GetTextColor(TextStyle.Muted);
            activeCardColor = sheet.GetImageColor(ImageRole.CardBackground);
            passiveCardColor = sheet.GetTextColor(TextStyle.Accent);
            passiveCardColor.a = 0.35f;
        }

        private void ResolveHandSlots()
        {
            if (slots.Count > 0)
            {
                return;
            }

            if (handSlotsRoot == null)
            {
                handSlotsRoot = transform.Find("Slots");
            }

            if (handHeaderText == null)
            {
                Transform header = transform.Find("Header");

                if (header != null)
                {
                    handHeaderText = header.GetComponent<TextMeshProUGUI>();
                }
            }

            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            if (sheet != null && handHeaderText != null)
            {
                sheet.ApplyOutline(handHeaderText);
            }

            if (handSlotsRoot == null)
            {
                return;
            }

            for (int i = 0; i < handSlotsRoot.childCount; i++)
            {
                Transform slot = handSlotsRoot.GetChild(i);
                Transform glyph = slot.Find("Glyph");
                TextMeshProUGUI label = glyph != null
                    ? glyph.GetComponent<TextMeshProUGUI>()
                    : slot.GetComponentInChildren<TextMeshProUGUI>(true);

                if (label == null)
                {
                    continue;
                }

                label.raycastTarget = false;
                label.enableAutoSizing = true;
                label.fontSizeMin = 8f;
                label.fontSizeMax = 14f;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.Center;

                UIStyleSheet slotSheet = UIStyleSheetLocator.Instance;

                if (slotSheet != null)
                {
                    slotSheet.ApplyOutline(label);
                }

                Image background = slot.GetComponent<Image>();
                Button button = slot.GetComponent<Button>();

                if (button == null)
                {
                    button = slot.gameObject.AddComponent<Button>();
                }

                button.transition = Selectable.Transition.None;

                if (background != null)
                {
                    background.raycastTarget = true;
                    button.targetGraphic = background;
                }

                int capturedIndex = slots.Count;
                button.onClick.AddListener(() => HandleSlotClicked(capturedIndex));

                slots.Add(new SlotView
                {
                    Label = label,
                    Background = background,
                    Button = button
                });
            }
        }

        private sealed class SlotView
        {
            public TextMeshProUGUI Label;
            public Image Background;
            public Button Button;
            public ActionCardHandEntry Entry;
            public ActionCardDefinition Definition;
            public bool HasCard;

            public CardActivationWindow ActivationWindow => Definition != null
                ? Definition.ActivationWindow
                : CardActivationWindow.AnyTime;

            public bool IsActiveCard => HasCard &&
                (Definition == null || Definition.Timing == CardTiming.Active);
        }
    }
}
