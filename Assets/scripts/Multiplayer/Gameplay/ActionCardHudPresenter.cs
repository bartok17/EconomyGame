using System.Collections.Generic;
using MonopolyGame.DesignSystem;
using TMPro;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Passive HUD view for the local player's action-card hand. The draw and
    /// discard piles are represented physically on the board.
    /// </summary>
    public sealed class ActionCardHudPresenter : MonoBehaviour
    {
        [Header("Hand")]
        [SerializeField] private Transform handSlotsRoot;
        [SerializeField] private TextMeshProUGUI handHeaderText;

        private readonly List<TextMeshProUGUI> handSlotLabels = new List<TextMeshProUGUI>();

        private MultiplayerGameSessionController session;
        private bool subscribed;
        private Color filledTextColor = Color.white;
        private Color emptyTextColor = new Color(0.65f, 0.65f, 0.65f);

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

        private void Subscribe()
        {
            if (subscribed || session == null)
            {
                return;
            }

            session.ActionCardsChanged += HandleActionCardsChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || session == null)
            {
                return;
            }

            session.ActionCardsChanged -= HandleActionCardsChanged;
            subscribed = false;
        }

        private void Refresh()
        {
            ResolveHandSlots();

            int capacity = session != null
                ? session.ActionCardHandCapacity
                : MonopolyGame.Cards.ActionCardHand.DefaultCapacity;
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

            for (int i = 0; i < handSlotLabels.Count; i++)
            {
                TextMeshProUGUI label = handSlotLabels[i];

                if (label == null)
                {
                    continue;
                }

                bool occupied = hand != null && i < hand.Count;
                label.text = occupied ? hand[i].ShortLabel.ToString() : "?";
                label.color = occupied ? filledTextColor : emptyTextColor;
            }
        }

        private void ResolveColors()
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;
            filledTextColor = sheet != null ? sheet.GetTextColor(TextStyle.Body) : Color.white;
            emptyTextColor = sheet != null
                ? sheet.GetTextColor(TextStyle.Muted)
                : new Color(0.65f, 0.65f, 0.65f);
        }

        private void ResolveHandSlots()
        {
            if (handSlotLabels.Count > 0)
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
                handSlotLabels.Add(label);
            }
        }
    }
}
