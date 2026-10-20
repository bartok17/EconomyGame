using System;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer.Gameplay;
using MonopolyGame.Pawns;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Camera
{
    /// <summary>
    /// Single entry point for everything that drives the board camera.
    /// Owns tile lookup, focus priority and mode switching so input clicks,
    /// session events and future features (trades, settings UI) never talk
    /// to the camera or resolve tiles on their own.
    /// Wired by <see cref="SceneManagement.GameSceneInstaller"/>.
    /// </summary>
    public sealed class BoardFocusCoordinator : MonoBehaviour, IBoardFocusCoordinator
    {
        [Header("Options")]
        [Tooltip("Automatically focus the tile a pawn lands on. Toggle at runtime (planned: keyboard shortcut / UI button).")]
        [SerializeField] private bool focusOnAction = true;

        private BoardCameraController camera;
        private MultiplayerGameSessionController session;
        private BoardManager board;
        private int lastFocusedIndex = -1;

        public bool FocusOnAction
        {
            get => focusOnAction;
            set => focusOnAction = value;
        }

        public bool IsBound => camera != null;

        // ── Wiring ─────────────────────────────────────────────────────────────

        public void Bind(
            BoardCameraController cameraController,
            MultiplayerGameSessionController sessionController,
            BoardManager boardManager)
        {
            Unbind();

            camera = cameraController;
            session = sessionController;
            board = boardManager;

            if (camera == null)
            {
                Debug.LogWarning($"[{nameof(BoardFocusCoordinator)}] No {nameof(BoardCameraController)} bound — camera focus is disabled.");
            }

            if (camera != null)
            {
                camera.SpaceClicked += HandleSpaceClicked;
            }

            if (session != null)
            {
                session.PawnMoved += HandlePawnMoved;
            }

            if (board != null)
            {
                board.SpacesRefreshed += HandleSpacesRefreshed;
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (camera != null)
            {
                camera.SpaceClicked -= HandleSpaceClicked;
            }

            if (session != null)
            {
                session.PawnMoved -= HandlePawnMoved;
            }

            if (board != null)
            {
                board.SpacesRefreshed -= HandleSpacesRefreshed;
            }

            camera = null;
            session = null;
            board = null;
        }

        // ── IBoardFocusCoordinator ─────────────────────────────────────────────

        public void FocusSpace(int boardIndex)
        {
            if (board == null)
            {
                Debug.LogWarning($"[{nameof(BoardFocusCoordinator)}] Cannot focus space {boardIndex}: no {nameof(BoardManager)} bound.");
                return;
            }

            BoardSpaceView space;
            try
            {
                space = board.GetSpace(boardIndex);
            }
            catch (InvalidOperationException)
            {
                Debug.LogWarning($"[{nameof(BoardFocusCoordinator)}] Board space {boardIndex} was not found.");
                return;
            }

            FocusSpace(space);
        }

        public void FocusSpace(BoardSpaceView space)
        {
            if (space == null || camera == null)
            {
                return;
            }

            // Already looking at this tile (e.g. pawn landed where the user is
            // inspecting) — don't yank the view back to preset angles.
            if (camera.FocusedSpace == space)
            {
                lastFocusedIndex = space.index;
                return;
            }

            lastFocusedIndex = space.index;
            camera.FocusSpace(space);
        }

        public void ClearFocus()
        {
            lastFocusedIndex = -1;
            camera?.ClearFocus();
        }

        public void ShowOverview()
        {
            lastFocusedIndex = -1;
            if (camera == null)
            {
                return;
            }

            camera.SetModeFreeOrbit();
            camera.ClearFocus();
        }

        public void ShowPlayerView(int playerIndex)
        {
            lastFocusedIndex = -1;
            camera?.SetModePlayerView(playerIndex);
        }

        public void ShowTradeView(int playerA, int playerB)
        {
            lastFocusedIndex = -1;
            camera?.SetModeTrade(playerA, playerB);
        }

        public void EndTradeView()
        {
            camera?.SetModeFreeOrbit();
        }

        // ── Event handlers ─────────────────────────────────────────────────────

        private void HandleSpaceClicked(BoardSpaceView space)
        {
            if (space != null)
            {
                FocusSpace(space);
            }
            else
            {
                ClearFocus();
            }
        }

        private void HandlePawnMoved(PlayerPawn pawn, int spaceIndex)
        {
            if (focusOnAction)
            {
                FocusSpace(spaceIndex);
            }
        }

        private void HandleSpacesRefreshed()
        {
            // Board was rebuilt: the previously focused tile may be a new object.
            // Re-resolve by index so the focus survives the refresh.
            if (board == null || lastFocusedIndex < 0)
            {
                return;
            }

            int index = lastFocusedIndex;
            lastFocusedIndex = -1;
            FocusSpace(index);
        }
    }
}
