using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using MonopolyGame.Pawns;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerBootstrapper : MonoBehaviour
    {
        [SerializeField] private MultiplayerFlowCoordinator flowCoordinator;

        private void Awake()
        {
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

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                var networkObject = new GameObject("NetworkManager");
                DontDestroyOnLoad(networkObject);
                networkManager = networkObject.AddComponent<NetworkManager>();
                networkObject.AddComponent<UnityTransport>();
            }

            if (networkManager.NetworkConfig == null)
            {
                networkManager.NetworkConfig = new NetworkConfig();
            }

            flowCoordinator.AssignNetworkManager(networkManager);

            PlayerPawnSpawner pawnSpawner = FindAnyObjectByType<PlayerPawnSpawner>();
            if (pawnSpawner != null)
            {
                pawnSpawner.RegisterNetworkPrefab(networkManager);
            }
        }
    }
}
