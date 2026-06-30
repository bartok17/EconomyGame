using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerBootstrapper : MonoBehaviour
    {
        private static MultiplayerBootstrapper _instance;

        [SerializeField] private MultiplayerFlowCoordinator flowCoordinator;

        [Header("Network")]
        [Tooltip("Assign the NetworkManager that is already in this scene. Never leave this empty — the bootstrapper will NOT create a new one.")]
        [SerializeField] private NetworkManager networkManager;

        private void Awake()
        {
            // Singleton: if we return to the auth scene a second time, destroy the
            // freshly-loaded duplicate so the original DDOL instance keeps running.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (!Application.isEditor)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }

            DontDestroyOnLoad(gameObject);

            if (flowCoordinator == null)
            {
                flowCoordinator = GetComponent<MultiplayerFlowCoordinator>();
            }

            if (flowCoordinator == null)
            {
                flowCoordinator = gameObject.AddComponent<MultiplayerFlowCoordinator>();
            }

            // Resolve NetworkManager: prefer inspector field, then singleton, then scene search.
            // NEVER create a new one — the scene's NetworkManager holds all prefab registrations.
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>();
            }

            if (networkManager == null)
            {
                Debug.LogError("[MultiplayerBootstrapper] No NetworkManager found in the scene. " +
                               "Add a NetworkManager to the Auth scene, configure its prefab list, " +
                               "and assign it to the 'Network Manager' field on this component.");
                return;
            }

            // Keep the scene's NetworkManager alive across scene loads.
            DontDestroyOnLoad(networkManager.gameObject);

            flowCoordinator.AssignNetworkManager(networkManager);
        }
    }
}
