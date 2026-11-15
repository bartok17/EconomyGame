using MonopolyGame.Multiplayer.Gameplay;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Replaces the blue event-deck placeholder on the table with two physical
    /// card piles and live count labels.
    /// </summary>
    public sealed class EventCardBoardView : CardPileBoardView
    {
        protected override string DeckAnchorPath => "Decor/EventDeck";
        protected override string PileCaptionPrefix => "EVENT";

        protected override int DrawCardCount => Session != null ? Session.EventCardDrawPileCount : 0;
        protected override int DiscardCardCount => Session != null ? Session.EventCardDiscardPileCount : 0;

        protected override void BindSessionEvents(MultiplayerGameSessionController session)
        {
            session.EventCardsChanged += Refresh;
        }

        protected override void UnbindSessionEvents(MultiplayerGameSessionController session)
        {
            session.EventCardsChanged -= Refresh;
        }

        private void Reset()
        {
            deckAnchor = transform.Find(DeckAnchorPath);
            drawPileColor = new Color(0.10f, 0.36f, 0.62f);
        }
    }
}
