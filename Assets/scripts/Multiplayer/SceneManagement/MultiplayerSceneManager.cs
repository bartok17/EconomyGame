using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Unity.Netcode;
using MonopolyGame.Multiplayer;
using MonopolyGame.Multiplayer.Gameplay;

namespace MonopolyGame.Multiplayer.SceneManagement
{
    public class MultiplayerSceneManager : MonoBehaviour
    {
        private static MultiplayerSceneManager _instance;

        [SerializeField] private string gameSceneName = "Game";
        [SerializeField] private CanvasGroup loadingScreenCanvasGroup;
        [SerializeField] private float fadeDuration = 0.3f;
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        private MonoBehaviour gameSceneInstaller;
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

        public void RegisterGameSceneInstaller(MonoBehaviour installer)
        {
            gameSceneInstaller = installer;
        }

        public void UnregisterGameSceneInstaller(MonoBehaviour installer)
        {
            if (gameSceneInstaller == installer)
            {
                gameSceneInstaller = null;
            }
        }

        private void OnReadyToEnterGame(MultiplayerRole role)
        {
            if (!isLoadingGame)
            {
                StartCoroutine(LoadGameSceneAsync());
            }
        }

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

            if (loadingScreenCanvasGroup != null)
            {
                loadingScreenCanvasGroup.gameObject.SetActive(true);
                yield return StartCoroutine(FadeCanvasGroup(loadingScreenCanvasGroup, 0, 1, fadeDuration));
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            bool useNetworkSceneManagement = networkManager != null && networkManager.IsListening;

            if (useNetworkSceneManagement && networkManager.IsServer)
            {
                networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            }
            else if (!useNetworkSceneManagement)
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);

                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }

            while (SceneManager.GetActiveScene().name != gameSceneName)
            {
                yield return null;
            }

            BindGameSceneInstaller();

            if (loadingScreenCanvasGroup != null)
            {
                yield return StartCoroutine(FadeCanvasGroup(loadingScreenCanvasGroup, 1, 0, fadeDuration));
                loadingScreenCanvasGroup.gameObject.SetActive(false);
            }

            isLoadingGame = false;
        }

        private void BindGameSceneInstaller()
        {
            if (gameSceneInstaller != null)
            {
                gameSceneInstaller.SendMessage("Configure", coordinator, SendMessageOptions.DontRequireReceiver);
                return;
            }

            Debug.LogWarning("[MultiplayerSceneManager] GameSceneInstaller was not found in the Game scene. Add the installer component to wire gameplay dependencies from the editor.");
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

        public void ReturnToLobby()
        {
            StopAllCoroutines();
            isLoadingGame = false;

            if (coordinator != null)
            {
                _ = coordinator.LeaveLobbyAsync();
            }

            SceneManager.LoadScene("AuthLobbyHub", LoadSceneMode.Single);
        }

        public static MultiplayerSceneManager Instance => _instance;
    }
}
