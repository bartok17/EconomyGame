using System;
using System.Collections.Generic;
using System.Text;
using MonopolyGame.Board;
using MonopolyGame.Cards;
using MonopolyGame.Debugging;
using MonopolyGame.Multiplayer.Debugging;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>Host-only console operations that answer with output lines or a failure message.</summary>
    public sealed partial class MultiplayerGameSessionController
    {
        [Header("Debugging")]
        [SerializeField] private bool enableDebugConsole = true;

        private DebugConsoleBridge debugConsoleBridge;
        private Coroutine debugLandCoroutine;
        private int debugForcedDice;

        public event Action<string, DebugCommandResult> DebugConsoleResponded;

        public DebugConsoleBridge DebugConsole => debugConsoleBridge ??= new DebugConsoleBridge(this);

        public bool DebugConsoleEnabled => enableDebugConsole;

        public int BoardSpaceCount => boardManager != null ? boardManager.SpaceCount : 0;

        public int JailSpaceIndex => jailSpaceIndex;

        public int PlayerCount => Registry.Count;

        public int GetPlayerBalance(int pawnSlot)
        {
            return _economyService != null ? _economyService.GetBalance(pawnSlot) : 0;
        }

        public void SendDebugCommandToServer(string line)
        {
            if (IsServer || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            DebugConsoleRequestServerRpc(line);
        }

        [ServerRpc(RequireOwnership = false)]
        private void DebugConsoleRequestServerRpc(string line, ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer)
            {
                return;
            }

            DebugCommandResult result;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            result = enableDebugConsole
                ? DebugConsole.ExecuteOnServer(line)
                : DebugCommandResult.Fail("The debug console is disabled on the host.");
#else
            result = DebugCommandResult.Fail("Debug commands are not available in this build.");
#endif

            DebugConsoleResultClientRpc(
                line,
                result.IsSuccess,
                string.Join("\n", result.Lines),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { serverRpcParams.Receive.SenderClientId }
                    }
                });
        }

        // The payload travels as one newline-joined string: Netcode's ILPP cannot
        // serialize string arrays, but plain strings are supported.
        [ClientRpc]
        private void DebugConsoleResultClientRpc(
            string line,
            bool success,
            string payload,
            ClientRpcParams clientRpcParams = default)
        {
            string[] lines = string.IsNullOrEmpty(payload)
                ? Array.Empty<string>()
                : payload.Split('\n');

            DebugCommandResult result = success
                ? DebugCommandResult.OkLines(lines)
                : DebugCommandResult.Fail(payload);

            DebugConsoleResponded?.Invoke(line, result);
        }

        public DebugCommandResult DebugSetBalance(int pawnSlot, int amount)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} is not in the game.");
            }

            _economyService.SetBalance(pawnSlot, amount);
            EconomyChanged?.Invoke();
            return DebugCommandResult.Ok($"{DebugPlayerName(pawnSlot)}: {amount}.");
        }

        public DebugCommandResult DebugAddBalance(int pawnSlot, int amount)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} is not in the game.");
            }

            _economyService.AddBalance(pawnSlot, amount);
            EconomyChanged?.Invoke();
            return DebugCommandResult.Ok($"{DebugPlayerName(pawnSlot)}: {GetPlayerBalance(pawnSlot)}.");
        }

        public DebugCommandResult DebugResetEconomy()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            var pawns = new List<PlayerPawnNetworkSync>();

            for (int i = 0; i < Registry.Count; i++)
            {
                pawns.Add(Registry.GetAtTurnIndex(i));
            }

            _turnResolution.InitializeEconomyState(pawns, BoardSpaceCount, startingBalance);
            ApplyPropertyOwnersToBoard();
            EconomyChanged?.Invoke();
            return DebugCommandResult.Ok($"Every balance is back to {startingBalance}.");
        }

        public DebugCommandResult DebugGrantCard(int pawnSlot, string cardId)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} is not in the game.");
            }

            ActionCardGrantResult grant = _actionCardService.GrantSpecificCard(pawnSlot, cardId);
            string playerName = DebugPlayerName(pawnSlot);

            switch (grant.Outcome)
            {
                case ActionCardGrantOutcome.Added:
                    return DebugCommandResult.Ok($"{playerName} received {DescribeCard(grant.Card)}.");

                case ActionCardGrantOutcome.HandFullDiscarded:
                    return DebugCommandResult.Ok(
                        $"{playerName}'s hand is full - {DescribeCard(grant.Card)} was discarded.");

                case ActionCardGrantOutcome.DeckEmpty:
                    return DebugCommandResult.Fail(string.IsNullOrWhiteSpace(cardId)
                        ? "No action cards are left in the deck."
                        : $"No '{cardId}' is left in the draw pile.");

                default:
                    return DebugCommandResult.Fail("That player has no hand.");
            }
        }

        public DebugCommandResult DebugClearHand(int pawnSlot)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!_actionCardService.ClearHand(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} has no hand.");
            }

            return DebugCommandResult.Ok($"{DebugPlayerName(pawnSlot)}'s hand was emptied.");
        }

        public DebugCommandResult DebugTriggerEvent(int pawnSlot)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} is not in the game.");
            }

            PlayerPawnNetworkSync pawn = Registry.FindBySlot(pawnSlot);
            EventCardDrawResult draw = TriggerEventCard(pawnSlot);

            if (draw.Outcome == EventCardOutcome.DeckEmpty)
            {
                return DebugCommandResult.Fail("The event deck is empty.");
            }

            string message = BuildEventCardMessage(pawn, draw);

            if (!string.IsNullOrEmpty(message))
            {
                _turnResolution.SetEconomyMessage(message);
            }

            return DebugCommandResult.Ok(message);
        }

        public DebugCommandResult DebugResetDecks()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            _actionCardService.ResetDeck();
            _eventCardService.ResetDeck();
            return DebugCommandResult.Ok("Both decks were rebuilt and every hand was emptied.");
        }

        public DebugCommandResult DebugTeleport(int pawnSlot, int spaceIndex)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            PlayerPawnNetworkSync pawn = Registry.FindBySlot(pawnSlot);

            if (pawn == null)
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} has no pawn.");
            }

            if (BoardSpaceCount <= 0)
            {
                return DebugCommandResult.Fail("The board has no spaces yet.");
            }

            pawn.ForceSpaceIndex(spaceIndex);
            return DebugCommandResult.Ok($"{DebugPlayerName(pawnSlot)} moved to {DescribeSpace(spaceIndex)}.");
        }

        public DebugCommandResult DebugMove(int pawnSlot, int spaces)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (spaces == 0)
            {
                return DebugCommandResult.Fail("Spaces must not be zero.");
            }

            if (isResolvingCard)
            {
                return DebugCommandResult.Fail("A card is already resolving.");
            }

            if (HasPendingPayment)
            {
                return DebugCommandResult.Fail("Finish the pending payment first.");
            }

            if (Registry.FindBySlot(pawnSlot) == null)
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} has no pawn.");
            }

            BeginCardResolution();
            pendingCardMoves.Add(new CardMoveRequest(pawnSlot, spaces));
            cardResolutionCoroutine = StartCoroutine(ResolveCardMovesRoutine());
            return DebugCommandResult.Ok(
                $"{DebugPlayerName(pawnSlot)} moves {spaces} {(Mathf.Abs(spaces) == 1 ? "space" : "spaces")}.");
        }

        public DebugCommandResult DebugLand(int pawnSlot, int spaceIndex)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (isResolvingCard)
            {
                return DebugCommandResult.Fail("A card is already resolving.");
            }

            if (HasPendingPayment)
            {
                return DebugCommandResult.Fail("Finish the pending payment first.");
            }

            if (Registry.FindBySlot(pawnSlot) == null)
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} has no pawn.");
            }

            if (BoardSpaceCount <= 0)
            {
                return DebugCommandResult.Fail("The board has no spaces yet.");
            }

            BeginCardResolution();

            if (debugLandCoroutine != null)
            {
                StopCoroutine(debugLandCoroutine);
            }

            debugLandCoroutine = StartCoroutine(DebugLandRoutine(pawnSlot, spaceIndex));
            return DebugCommandResult.Ok($"{DebugPlayerName(pawnSlot)} lands on {DescribeSpace(spaceIndex)}.");
        }

        private System.Collections.IEnumerator DebugLandRoutine(int pawnSlot, int spaceIndex)
        {
            try
            {
                PlayerPawnNetworkSync pawn = Registry.FindBySlot(pawnSlot);

                if (pawn == null)
                {
                    yield break;
                }

                MovePawnClientRpc(pawnSlot, spaceIndex, pawnMoveDuration);
                yield return ResolveLandingEffectsRoutine(pawn, spaceIndex, passedStart: false, changeTurnPhase: false);
            }
            finally
            {
                EndCardResolution();
                debugLandCoroutine = null;
            }
        }

        public DebugCommandResult DebugSetPropertyOwner(int spaceIndex, int ownerPawnSlot)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsPropertySpace(spaceIndex))
            {
                return DebugCommandResult.Fail($"Space {spaceIndex} is not a property.");
            }

            if (!IsSeated(ownerPawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {ownerPawnSlot} is not in the game.");
            }

            PlayerEconomyState owner = _economyService.GetPlayerState(ownerPawnSlot);
            string ownerName = DebugPlayerName(ownerPawnSlot);
            _economyService.SetPropertyOwner(spaceIndex, ownerPawnSlot, owner.PlayerId.ToString(), ownerName);
            return DebugCommandResult.Ok($"{DescribeSpace(spaceIndex)} now belongs to {ownerName}.");
        }

        public DebugCommandResult DebugClearProperty(int spaceIndex)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            bool removed = false;

            for (int i = 0; i < propertyOwnershipNet.Count; i++)
            {
                if (propertyOwnershipNet[i].SpaceIndex != spaceIndex)
                {
                    continue;
                }

                propertyOwnershipNet.RemoveAt(i);
                removed = true;
                break;
            }

            boardManager?.SetSpaceOwner(spaceIndex, string.Empty, -1);
            boardManager?.SetSpaceBuildings(spaceIndex, 0);

            return removed
                ? DebugCommandResult.Ok($"{DescribeSpace(spaceIndex)} is back with the bank.")
                : DebugCommandResult.Fail($"{DescribeSpace(spaceIndex)} is not owned.");
        }

        public DebugCommandResult DebugGrantAllProperties(int ownerPawnSlot)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(ownerPawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {ownerPawnSlot} is not in the game.");
            }

            PlayerEconomyState owner = _economyService.GetPlayerState(ownerPawnSlot);
            string ownerName = DebugPlayerName(ownerPawnSlot);
            int granted = 0;

            for (int i = 0; i < BoardSpaceCount; i++)
            {
                if (!IsPropertySpace(i))
                {
                    continue;
                }

                _economyService.SetPropertyOwner(i, ownerPawnSlot, owner.PlayerId.ToString(), ownerName);
                granted++;
            }

            return DebugCommandResult.Ok($"{ownerName} now owns all {granted} properties.");
        }

        public DebugCommandResult DebugSetPropertyHouses(int spaceIndex, int level)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsPropertySpace(spaceIndex))
            {
                return DebugCommandResult.Fail($"Space {spaceIndex} is not a property.");
            }

            if (!_economyService.IsPropertyOwned(spaceIndex))
            {
                return DebugCommandResult.Fail($"{DescribeSpace(spaceIndex)} has no owner. Use 'own <space> <player>' first.");
            }

            int clamped = PropertyBuildingRules.ClampLevel(level);
            _economyService.SetPropertyHouses(spaceIndex, clamped);
            return DebugCommandResult.Ok($"{DescribeSpace(spaceIndex)} now has {DescribeLevel(clamped)}.");
        }

        public DebugCommandResult DebugBuildHouse(int spaceIndex, int count)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsPropertySpace(spaceIndex))
            {
                return DebugCommandResult.Fail($"Space {spaceIndex} is not a property.");
            }

            PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(CurrentTurnIndex);

            if (pawn == null)
            {
                return DebugCommandResult.Fail("The active player has no pawn.");
            }

            int built = 0;
            string message = string.Empty;

            for (int i = 0; i < count; i++)
            {
                if (!BuildingService.TryBuild(spaceIndex, pawn, out message))
                {
                    break;
                }

                built++;
            }

            if (built == 0)
            {
                return DebugCommandResult.Fail(message);
            }

            _turnResolution.SetEconomyMessage(message);
            return DebugCommandResult.Ok($"{message} ({built} build(s))");
        }

        public DebugCommandResult DebugSellHouse(int spaceIndex, int count)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsPropertySpace(spaceIndex))
            {
                return DebugCommandResult.Fail($"Space {spaceIndex} is not a property.");
            }

            PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(CurrentTurnIndex);

            if (pawn == null)
            {
                return DebugCommandResult.Fail("The active player has no pawn.");
            }

            int sold = 0;
            string message = string.Empty;

            for (int i = 0; i < count; i++)
            {
                if (!BuildingService.TrySell(spaceIndex, pawn, out message))
                {
                    break;
                }

                sold++;
            }

            if (sold == 0)
            {
                return DebugCommandResult.Fail(message);
            }

            _turnResolution.SetEconomyMessage(message);
            return DebugCommandResult.Ok($"{message} ({sold} sale(s))");
        }

        public DebugCommandResult DebugSetActiveTurn(int pawnSlot)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} is not in the game.");
            }

            ResetTransientTurnState();
            turnStateMachine.SetParticipants(Registry.BuildParticipants());
            turnStateMachine.StartGame(pawnSlot);
            SetServerState(turnStateMachine.State);
            return DebugCommandResult.Ok($"Turn forced to {DebugPlayerName(pawnSlot)}.");
        }

        public DebugCommandResult DebugSetPhase(TurnPhase phase)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (phase != TurnPhase.AwaitingRoll && phase != TurnPhase.WaitingForEndTurn)
            {
                return DebugCommandResult.Fail("Only 'awaitingroll' and 'waitingforendturn' can be forced.");
            }

            turnStateMachine.SetState(new TurnState(
                CurrentTurnIndex,
                phase,
                LastDiceRoll,
                ActivePlayerName,
                ActiveClientId,
                true));
            SetServerState(turnStateMachine.State);
            return DebugCommandResult.Ok($"Phase forced to {phase}.");
        }

        public DebugCommandResult DebugRoll(int forcedDice)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (isResolvingCard)
            {
                return DebugCommandResult.Fail("A card is resolving.");
            }

            if (HasPendingPayment)
            {
                return DebugCommandResult.Fail("Finish the pending payment first.");
            }

            if (CurrentPhase != TurnPhase.AwaitingRoll)
            {
                return DebugCommandResult.Fail("The turn has already rolled. Use 'turn <slot>' or 'phase awaitingroll' first.");
            }

            if (forcedDice != 0 && (forcedDice < 1 || forcedDice > 6))
            {
                return DebugCommandResult.Fail("The dice value must be between 1 and 6.");
            }

            debugForcedDice = forcedDice;
            HandleRollOnServer(turnStateMachine.GetParticipantClientId(CurrentTurnIndex));
            return DebugCommandResult.Ok(forcedDice > 0 ? $"Rolling a forced {forcedDice}." : "Rolling.");
        }

        public DebugCommandResult DebugEndTurn()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (isResolvingCard)
            {
                return DebugCommandResult.Fail("A card is resolving.");
            }

            if (HasPendingPayment)
            {
                return DebugCommandResult.Fail("Finish the pending payment first.");
            }

            ResetTransientTurnState();
            AdvanceTurnServer();
            return DebugCommandResult.Ok($"Turn ended. Now: {ActivePlayerName}.");
        }

        public DebugCommandResult DebugResetTurn()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            ResetTransientTurnState();
            turnStateMachine.SetParticipants(Registry.BuildParticipants());
            turnStateMachine.StartGame(0);
            SetServerState(turnStateMachine.State);
            return DebugCommandResult.Ok($"Turn reset to {DebugPlayerName(0)}.");
        }

        public DebugCommandResult DebugResetBoard()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            propertyOwnershipNet.Clear();
            pendingPurchaseSpaceIndexNet.Value = -1;

            if (boardManager != null)
            {
                for (int i = 0; i < boardManager.SpaceCount; i++)
                {
                    boardManager.SetSpaceOwner(i, string.Empty, -1);
                    boardManager.SetSpaceBuildings(i, 0);
                }
            }

            for (int slot = 0; slot < Registry.Count; slot++)
            {
                _economyService.SetHousesBuiltThisLap(slot, 0);
            }

            EconomyChanged?.Invoke();
            return DebugCommandResult.Ok("Every property is back with the bank, buildings cleared, lap counters reset.");
        }

        public DebugCommandResult DebugDescribeState()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            var lines = new List<string>
            {
                $"Turn: {DebugPlayerName(CurrentTurnIndex)} (slot {CurrentTurnIndex})  Phase: {CurrentPhase}  Dice: {LastDiceRoll}",
                $"Card resolving: {(isResolvingCard ? "yes" : "no")}  Pending payment: {(HasPendingPayment ? "yes" : "no")}  Queued payments: {queuedPayments.Count}",
                DescribePile("Action", actionCardPileStateNet.Value),
                DescribePile("Event", eventCardPileStateNet.Value),
                "Players:"
            };

            for (int slot = 0; slot < Registry.Count; slot++)
            {
                PlayerPawnNetworkSync pawn = Registry.FindBySlot(slot);
                int space = pawn != null ? pawn.CurrentSpaceIndex : -1;
                string hand = DescribeHandInline(slot);
                int lapLimit = HousesPerLapLimit;
                string lap = lapLimit > 0 ? $"{_economyService.GetHousesBuiltThisLap(slot)}/{lapLimit}" : "unlimited";
                lines.Add($"  #{slot} {DebugPlayerName(slot),-12} {GetPlayerBalance(slot),6}  {DescribeSpace(space),-22} lap builds: {lap}  hand: {hand}");
            }

            int owned = 0;

            for (int i = 0; i < propertyOwnershipNet.Count; i++)
            {
                if (propertyOwnershipNet[i].OwnerPawnSlot >= 0)
                {
                    owned++;
                }
            }

            lines.Add($"Owned properties: {owned}");
            return DebugCommandResult.OkLines(lines);
        }

        public DebugCommandResult DebugDescribePlayers()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            var lines = new List<string>();

            for (int slot = 0; slot < Registry.Count; slot++)
            {
                PlayerPawnNetworkSync pawn = Registry.FindBySlot(slot);
                int space = pawn != null ? pawn.CurrentSpaceIndex : -1;
                lines.Add($"#{slot} {DebugPlayerName(slot),-12} {GetPlayerBalance(slot),6}  {DescribeSpace(space)}");
            }

            return DebugCommandResult.OkLines(lines);
        }

        public DebugCommandResult DebugDescribeHand(int pawnSlot)
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!IsSeated(pawnSlot))
            {
                return DebugCommandResult.Fail($"Player slot {pawnSlot} is not in the game.");
            }

            IReadOnlyList<ActionCardHandEntry> entries = _actionCardService.GetHandEntries(pawnSlot);
            var lines = new List<string> { $"{DebugPlayerName(pawnSlot)}'s hand ({entries.Count}):" };

            for (int i = 0; i < entries.Count; i++)
            {
                lines.Add(
                    $"  {entries[i].DisplayName} [{entries[i].CardId}] - {entries[i].Description}");
            }

            if (entries.Count == 0)
            {
                lines.Add("  (empty)");
            }

            return DebugCommandResult.OkLines(lines);
        }

        public DebugCommandResult DebugDescribePiles()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return DebugCommandResult.Ok(
                DescribePile("Action", actionCardPileStateNet.Value),
                DescribePile("Event", eventCardPileStateNet.Value));
        }

        public DebugCommandResult DebugDescribeBoard()
        {
            if (!TryPrepareDebug(out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            var lines = new List<string>();

            for (int i = 0; i < BoardSpaceCount; i++)
            {
                if (!IsPropertySpace(i))
                {
                    continue;
                }

                BoardSpaceView space = boardManager.GetSpace(i);
                int ownerSlot = _economyService.GetPropertyOwnerPawnSlot(i);
                string owner = ownerSlot >= 0 ? DebugPlayerName(ownerSlot) : "-";
                lines.Add($"[{i,2}] {space.displayName,-16} price {space.price,4}  owner {owner,-12} {DescribeLevel(_economyService.GetPropertyHouses(i))}");
            }

            if (lines.Count == 0)
            {
                lines.Add("The board has no properties.");
            }

            return DebugCommandResult.OkLines(lines);
        }

        private bool TryPrepareDebug(out string error)
        {
            if (!IsServer)
            {
                error = "Debug commands only run on the host.";
                return false;
            }

            if (!IsInitialized || Registry.Count == 0)
            {
                error = "The game has not started yet.";
                return false;
            }

            error = null;
            return true;
        }

        private bool IsSeated(int pawnSlot)
        {
            return pawnSlot >= 0 && pawnSlot < Registry.Count && Registry.FindBySlot(pawnSlot) != null;
        }

        private bool IsPropertySpace(int spaceIndex)
        {
            if (boardManager == null || spaceIndex < 0 || spaceIndex >= boardManager.SpaceCount)
            {
                return false;
            }

            BoardSpaceView space = boardManager.GetSpace(spaceIndex);
            return space != null && space.type == BoardSpaceType.Property;
        }

        private string DescribeSpace(int spaceIndex)
        {
            if (spaceIndex < 0)
            {
                return "(unknown)";
            }

            string name = GetBoardSpaceName(spaceIndex);
            return $"{spaceIndex} ({name})";
        }

        private static string DescribeLevel(int level)
        {
            if (PropertyBuildingRules.IsHotel(level))
            {
                return "hotel";
            }

            return level > 0 ? $"{level} houses" : "-";
        }

        private string DescribeHandInline(int pawnSlot)
        {
            IReadOnlyList<ActionCardHandEntry> entries = _actionCardService.GetHandEntries(pawnSlot);

            if (entries.Count == 0)
            {
                return "-";
            }

            var builder = new StringBuilder();

            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(entries[i].ShortLabel.ToString().Replace('\n', ' '));
            }

            return builder.ToString();
        }

        private static string DescribePile(string label, CardPileState state)
        {
            string top = state.TopDiscardDisplayName.ToString();
            string topLabel = string.IsNullOrEmpty(top) ? "empty" : top;
            return $"{label} deck: {state.DrawPileCount} draw / {state.DiscardPileCount} discard, top discard: {topLabel}";
        }

        private static string DescribeCard(ActionCardInstance card)
        {
            return card != null && card.Definition != null
                ? card.Definition.DisplayName
                : "a card";
        }

        private string DebugPlayerName(int pawnSlot)
        {
            PlayerPawnNetworkSync pawn = Registry.FindBySlot(pawnSlot);

            if (pawn != null && !string.IsNullOrWhiteSpace(pawn.DisplayName))
            {
                return pawn.DisplayName;
            }

            if (_economyService != null)
            {
                string stored = _economyService.GetPlayerState(pawnSlot).DisplayName.ToString();

                if (!string.IsNullOrWhiteSpace(stored))
                {
                    return stored;
                }
            }

            return $"Player {pawnSlot + 1}";
        }

        private void ResetTransientTurnState()
        {
            pendingPurchaseSpaceIndexNet.Value = -1;
            pendingCardMoves.Clear();
            queuedPayments.Clear();
            pendingPaymentNet.Value = default;
            debugForcedDice = 0;

            if (turnCoroutine != null)
            {
                StopCoroutine(turnCoroutine);
                turnCoroutine = null;
            }

            if (cardResolutionCoroutine != null)
            {
                StopCoroutine(cardResolutionCoroutine);
                cardResolutionCoroutine = null;
            }

            if (debugLandCoroutine != null)
            {
                StopCoroutine(debugLandCoroutine);
                debugLandCoroutine = null;
            }

            if (isResolvingCard || cardResolutionNet.Value)
            {
                EndCardResolution();
            }
        }
    }
}
