using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
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

        public static GameSceneInstaller Instance => instance;

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
        }
    }
}