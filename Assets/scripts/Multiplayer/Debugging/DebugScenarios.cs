using System.Collections.Generic;
using MonopolyGame.Debugging;

namespace MonopolyGame.Multiplayer.Debugging
{
    /// <summary>Built-in scripts that jump the session to a state that would take a full game to reach.</summary>
    public static class DebugScenarios
    {
        public static List<DebugScenario> CreateDefault()
        {
            return new List<DebugScenario>
            {
                new DebugScenario(
                    "late-game",
                    "Rich players, owned properties and cards in hand.",
                    "money all 7500",
                    "own 1 0",
                    "own 3 0",
                    "own 6 0",
                    "own 8 0",
                    "own 9 1",
                    "own 11 1",
                    "own 13 1",
                    "own 14 1",
                    "givecard 0 sprinter",
                    "givecard 0 guard",
                    "givecard 1 windfall"),

                new DebugScenario(
                    "bankrupt",
                    "Player 0 lands on player 1's property with 25 left, so a manual payment is due.",
                    "reset turn",
                    "money 0 25",
                    "own 6 1",
                    "land 0 6"),

                new DebugScenario(
                    "cardlab",
                    "Fresh decks and one of every card type in each hand.",
                    "reset decks",
                    "givecard all sprinter",
                    "givecard all windfall",
                    "givecard all guard"),

                new DebugScenario(
                    "endgame",
                    "Player 0 owns everything and the others are nearly broke.",
                    "money 0 12000",
                    "money 1 120",
                    "ownall 0")
            };
        }
    }
}
