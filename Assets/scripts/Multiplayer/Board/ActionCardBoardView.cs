using MonopolyGame.Multiplayer.Gameplay;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Replaces the red action-deck placeholder on the table with two physical
    /// card piles and live count labels.
    /// </summary>
    public sealed class ActionCardBoardView : CardPileBoardView
    {
        protected override string DeckAnchorPath => "Decor/ActionDeck";
        protected override string PileCaptionPrefix => "ACTION";

        protected override int DrawCardCount => Session != null ? Session.ActionCardDrawPileCount : 0;
        protected override int DiscardCardCount => Session != null ? Session.ActionCardDiscardPileCount : 0;

        protected override void BindSessionEvents(MultiplayerGameSessionController session)
        {
            session.ActionCardsChanged += Refresh;
        }

        protected override void UnbindSessionEvents(MultiplayerGameSessionController session)
        {
            session.ActionCardsChanged -= Refresh;
        }
    }
}
