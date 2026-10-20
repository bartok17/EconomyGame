using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
using MonopolyGame.Multiplayer.Camera;
using MonopolyGame.Multiplayer.Gameplay;
using MonopolyGame.Pawns;
using UnityEngine;

namespace MonopolyGame.Multiplayer.SceneManagement
{
    public sealed class GameSceneInstaller : MonoBehaviour
    {
        private static GameSceneInstaller instance;

        [Header("Scene References")]
        [SerializeField] private MultiplayerGameSessionController sessionController;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private PlayerPawnSpawner pawnSpawner;
        [SerializeField] private MultiplayerGameHudPresenter hudPresenter;
        [SerializeField] private BoardCameraController boardCamera;
        [SerializeField] private BoardFocusCoordinator focusCoordinator;

        public static GameSceneInstaller Instance => instance;

        public BoardCameraController BoardCamera => boardCamera;

        public BoardFocusCoordinator FocusCoordinator => focusCoordinator;

        private void OnEnable()
        {
            instance = this;

            if (MultiplayerSceneManager.Instance != null)
            {
                MultiplayerSceneManager.Instance.RegisterGameSceneInstaller(this);
            }
        }

        private void OnDisable()
        {
            if (MultiplayerSceneManager.Instance != null)
            {
                MultiplayerSceneManager.Instance.UnregisterGameSceneInstaller(this);
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void Awake()
        {
            AutoWireSceneReferences();
        }

        private void OnValidate()
        {
            AutoWireSceneReferences();
        }

        public void Configure(MultiplayerFlowCoordinator coordinator)
        {
            AutoWireSceneReferences();

            if (coordinator == null)
            {
                Debug.LogError($"[{nameof(GameSceneInstaller)}] Cannot configure the game scene without a flow coordinator.");
                return;
            }

            if (sessionController == null || boardManager == null || pawnSpawner == null)
            {
                Debug.LogError($"[{nameof(GameSceneInstaller)}] Missing scene references. Session, board, and pawn spawner must be assigned.");
                return;
            }

            sessionController.BindDependencies(coordinator, boardManager, pawnSpawner);
            pawnSpawner.BindDependencies(boardManager, coordinator);

            if (hudPresenter != null)
            {
                hudPresenter.BindSession(sessionController);
            }

            if (focusCoordinator != null)
            {
                focusCoordinator.Bind(boardCamera, sessionController, boardManager);
            }
        }

        private void AutoWireSceneReferences()
        {
            if (sessionController == null)
            {
                sessionController = GetComponent<MultiplayerGameSessionController>();
            }

            if (hudPresenter == null)
            {
                hudPresenter = GetComponent<MultiplayerGameHudPresenter>();
            }

            if (boardCamera == null)
            {
                boardCamera = GetComponent<BoardCameraController>();

                if (boardCamera == null)
                {
                    boardCamera = FindAnyObjectByType<BoardCameraController>();
                }
            }

            if (focusCoordinator == null)
            {
                focusCoordinator = GetComponent<BoardFocusCoordinator>();

                if (focusCoordinator == null)
                {
                    focusCoordinator = FindAnyObjectByType<BoardFocusCoordinator>();
                }
            }
        }
    }
}