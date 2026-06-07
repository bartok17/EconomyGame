using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using MonopolyGame.Multiplayer;

namespace MonopolyGame.Multiplayer.SceneManagement
{
    /// <summary>
    /// Manages scene transitions for the multiplayer lobby and game flow.
    /// Listens to MultiplayerFlowCoordinator events and loads appropriate scenes.
    /// </summary>
    public class MultiplayerSceneManager : MonoBehaviour
    {
        private static MultiplayerSceneManager _instance;

        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private CanvasGroup loadingScreenCanvasGroup;
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        private bool isLoadingGame = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (coordinator != null)
            {
                coordinator.ReadyToEnterGame += OnReadyToEnterGame;
            }
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.ReadyToEnterGame -= OnReadyToEnterGame;
            }
        }

        /// <summary>
        /// Called when multiplayer network is ready and players can enter game.
        /// </summary>
        private void OnReadyToEnterGame(MultiplayerRole role)
        {
            if (!isLoadingGame)
            {
                StartCoroutine(LoadGameSceneAsync());
            }
        }

        /// <summary>
        /// Manually trigger game scene load (for testing or explicit control).
        /// </summary>
        public void TriggerLoadGameScene()
        {
            if (!isLoadingGame)
            {
                StartCoroutine(LoadGameSceneAsync());
            }
        }

        private IEnumerator LoadGameSceneAsync()
        {
            isLoadingGame = true;

            // Show loading screen with fade-in
            if (loadingScreenCanvasGroup != null)
            {
                loadingScreenCanvasGroup.gameObject.SetActive(true);
                yield return StartCoroutine(FadeCanvasGroup(loadingScreenCanvasGroup, 0, 1, fadeDuration));
            }

            // Load game scene asynchronously
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // Fade out loading screen
            if (loadingScreenCanvasGroup != null)
            {
                yield return StartCoroutine(FadeCanvasGroup(loadingScreenCanvasGroup, 1, 0, fadeDuration));
                loadingScreenCanvasGroup.gameObject.SetActive(false);
            }

            isLoadingGame = false;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float start, float end, float duration)
        {
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }

            canvasGroup.alpha = end;
        }

        /// <summary>
        /// Return to lobby (called when leaving game or on disconnect).
        /// </summary>
        public void ReturnToLobby()
        {
            StopAllCoroutines();
            isLoadingGame = false;

            // Call leave lobby on flow coordinator
            if (coordinator != null)
            {
                _ = coordinator.LeaveLobbyAsync();
            }

            // Reload auth/lobby hub scene
            SceneManager.LoadScene("AuthLobbyHub", LoadSceneMode.Single);
        }

        public static MultiplayerSceneManager Instance => _instance;
    }
}
